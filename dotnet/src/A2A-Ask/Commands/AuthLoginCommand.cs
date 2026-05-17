using System.CommandLine;
using System.CommandLine.Invocation;
using A2A;
using A2AAsk.Auth;
using A2AAsk.Output;

namespace A2AAsk.Commands;

public static class AuthCommand
{
    public static Command Create()
    {
        var authCommand = new Command("auth", "Authentication management");
        authCommand.AddCommand(CreateLoginCommand());
        authCommand.AddCommand(CreateLogoutCommand());
        authCommand.AddCommand(CreateStatusCommand());
        authCommand.AddCommand(CreateRegisterClientCommand());
        authCommand.AddCommand(CreateListClientsCommand());
        authCommand.AddCommand(CreateRemoveClientCommand());
        return authCommand;
    }

    private static Command CreateLoginCommand()
    {
        var urlArgument = new Argument<string>(
            name: "url",
            description: "Agent base URL to authenticate against");

        var clientIdOption = CommonOptions.ClientId();
        var clientSecretOption = CommonOptions.ClientSecret();
        var tenantOption = CommonOptions.Tenant();

        var command = new Command("login", "Interactively authenticate with an A2A agent")
        {
            urlArgument, clientIdOption, clientSecretOption, tenantOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var clientId = context.ParseResult.GetValueForOption(clientIdOption);
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
            var tenant = context.ParseResult.GetValueForOption(tenantOption);
            var verbose = context.ParseResult.GetValueForOption(
                context.ParseResult.RootCommandResult.Command.Options
                    .OfType<Option<bool>>().First(o => o.Name == "verbose"));

            try
            {
                var baseUri = new Uri(url.TrimEnd('/'));

                var resolver = new A2ACardResolver(baseUri);
                var card = await resolver.GetAgentCardAsync(context.GetCancellationToken());

                if (card.SecuritySchemes == null || card.SecuritySchemes.Count == 0)
                {
                    Console.WriteLine("This agent does not require authentication.");
                    return;
                }

                Console.WriteLine($"Agent: {card.Name}");
                Console.WriteLine("Security schemes available:");
                foreach (var (name, scheme) in card.SecuritySchemes)
                {
                    var schemeType = GetSchemeType(scheme);
                    Console.WriteLine($"  {name}: {schemeType}");
                }
                Console.WriteLine();

                var storageKey = TokenStore.BuildStorageKey(url, tenant);

                if (!string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
                {
                    var oauth2Scheme = FindOAuth2Scheme(card);
                    if (!oauth2Scheme.HasValue)
                    {
                        Console.Error.WriteLine("No OAuth2 scheme found in agent card for client credentials.");
                        context.ExitCode = 1;
                        return;
                    }

                    Console.WriteLine("Using OAuth2 client_credentials flow...");
                    var tokenResult = await ClientCredentialsFlow.AuthenticateAsync(
                        oauth2Scheme.Value.Scheme,
                        clientId,
                        clientSecret,
                        cancellationToken: context.GetCancellationToken());

                    if (tokenResult != null)
                    {
                        var store = new TokenStore();
                        await store.SaveTokenAsync(storageKey, tokenResult);
                        Console.WriteLine();
                        Console.WriteLine($"Authenticated successfully. Token stored for {url}");
                    }
                    else
                    {
                        context.ExitCode = 1;
                    }
                    return;
                }

                var oauth2 = FindOAuth2Scheme(card);
                if (oauth2.HasValue)
                {
                    var (schemeName, scheme) = oauth2.Value;
                    var requiredScopes = ExtractRequiredScopes(card, schemeName);
                    var effectiveClientId = clientId;
                    string? resource = null;

                    if (string.IsNullOrWhiteSpace(effectiveClientId))
                    {
                        var issuer = await ResolveIssuerAsync(scheme, context.GetCancellationToken());
                        if (!string.IsNullOrWhiteSpace(issuer))
                        {
                            var registrationStore = new ClientRegistrationStore();
                            var registration = await registrationStore.FindClientAsync(issuer);
                            if (registration != null)
                            {
                                effectiveClientId = registration.ClientId;
                                resource = registration.Resource;
                                Console.WriteLine($"Using registered client '{registration.ClientId}' for issuer: {registration.Issuer}");
                                if (!string.IsNullOrWhiteSpace(resource))
                                    Console.WriteLine($"Using resource: {resource}");
                                Console.WriteLine();
                            }
                        }
                    }

                    TokenResult? tokenResult;
                    if (scheme.Flows?.DeviceCode != null)
                    {
                        Console.WriteLine($"Using OAuth2 device code flow via '{schemeName}'...");
                        Console.WriteLine();
                        var flow = new DeviceCodeFlow(scheme);
                        tokenResult = await flow.AuthenticateAsync(
                            requiredScopes,
                            effectiveClientId,
                            resource,
                            context.GetCancellationToken());
                    }
                    else if (scheme.Flows?.AuthorizationCode != null)
                    {
                        Console.WriteLine($"Using OAuth2 authorization code flow via '{schemeName}'...");
                        Console.WriteLine("Opening browser for authentication...");
                        Console.WriteLine();
                        tokenResult = await AuthCodeFlow.AuthenticateAsync(
                            scheme,
                            requiredScopes,
                            effectiveClientId,
                            resource,
                            context.GetCancellationToken());
                    }
                    else
                    {
                        Console.Error.WriteLine("No supported interactive OAuth2 flow found (need device_code or authorization_code).");
                        context.ExitCode = 1;
                        return;
                    }

                    if (tokenResult != null)
                    {
                        var store = new TokenStore();
                        await store.SaveTokenAsync(storageKey, tokenResult);
                        Console.WriteLine();
                        Console.WriteLine($"Authenticated successfully. Token stored for {url}");
                        Console.WriteLine("Subsequent a2a-ask commands will use the stored token automatically.");
                    }
                    else
                    {
                        Console.Error.WriteLine("Authentication failed.");
                        context.ExitCode = 1;
                    }
                }
                else
                {
                    Console.WriteLine("No OAuth2 flow available for interactive login.");
                    Console.WriteLine("To authenticate, use one of the following options with your commands:");
                    Console.WriteLine();

                    foreach (var (_, scheme) in card.SecuritySchemes)
                    {
                        if (scheme.SchemeCase == SecuritySchemeCase.HttpAuth)
                        {
                            var http = scheme.HttpAuthSecurityScheme!;
                            if (string.Equals(http.Scheme, "basic", StringComparison.OrdinalIgnoreCase))
                                Console.WriteLine("  --auth-user <username> --auth-password <password>");
                            else
                                Console.WriteLine($"  --auth-token <your-{http.Scheme ?? "bearer"}-token>");
                        }
                        else if (scheme.SchemeCase == SecuritySchemeCase.ApiKey)
                        {
                            var apiKey = scheme.ApiKeySecurityScheme!;
                            Console.WriteLine($"  --api-key <your-key> --api-key-header {apiKey.Name}");
                        }
                        else if (scheme.SchemeCase == SecuritySchemeCase.OpenIdConnect)
                        {
                            var oidc = scheme.OpenIdConnectSecurityScheme!;
                            Console.WriteLine("  --auth-token <token-from-oidc-provider>");
                            Console.WriteLine($"    OIDC Discovery: {oidc.OpenIdConnectUrl}");
                        }
                    }

                    context.ExitCode = 1;
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateRegisterClientCommand()
    {
        var clientIdOption = new Option<string>("--client-id", "OAuth2 client ID to register")
        {
            IsRequired = true
        };
        var issuerOption = new Option<string>("--issuer", "OAuth2 issuer URL to match")
        {
            IsRequired = true
        };
        var resourceOption = new Option<string?>("--resource", "Optional RFC 8707 resource URL");

        var command = new Command("register-client", "Register an OAuth2 client for an issuer")
        {
            clientIdOption, issuerOption, resourceOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var clientId = context.ParseResult.GetValueForOption(clientIdOption);
            var issuer = context.ParseResult.GetValueForOption(issuerOption);
            var resource = context.ParseResult.GetValueForOption(resourceOption);

            try
            {
                var normalizedIssuer = ClientRegistrationStore.NormalizeIssuer(issuer!);
                var store = new ClientRegistrationStore();
                await store.RegisterClientAsync(new ClientRegistration
                {
                    ClientId = clientId!,
                    Issuer = normalizedIssuer,
                    Resource = resource,
                    CreatedAt = DateTime.UtcNow
                });

                Console.WriteLine($"Client registered for issuer: {normalizedIssuer}");
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, false);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateListClientsCommand()
    {
        var command = new Command("list-clients", "List registered OAuth2 clients");

        command.SetHandler(async (InvocationContext context) =>
        {
            try
            {
                var store = new ClientRegistrationStore();
                var registrations = await store.ListClientsAsync();
                if (registrations.Count == 0)
                {
                    Console.WriteLine("No registered clients.");
                    return;
                }

                var rows = registrations
                    .Select(r => new[]
                    {
                        r.Issuer,
                        r.ClientId,
                        r.Resource ?? "—",
                        r.CreatedAt.ToUniversalTime().ToString("u")
                    })
                    .ToList();

                WriteTable(
                    ["Issuer", "Client ID", "Resource", "Registered"],
                    rows);
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, false);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateRemoveClientCommand()
    {
        var issuerOption = new Option<string>("--issuer", "OAuth2 issuer URL to remove")
        {
            IsRequired = true
        };
        var resourceOption = new Option<string?>("--resource", "Optional RFC 8707 resource URL");

        var command = new Command("remove-client", "Remove a registered OAuth2 client")
        {
            issuerOption, resourceOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var issuer = context.ParseResult.GetValueForOption(issuerOption);
            var resource = context.ParseResult.GetValueForOption(resourceOption);

            try
            {
                var normalizedIssuer = ClientRegistrationStore.NormalizeIssuer(issuer!);
                var normalizedResource = ClientRegistrationStore.NormalizeResource(resource);
                var store = new ClientRegistrationStore();
                var removed = await store.RemoveClientAsync(normalizedIssuer, normalizedResource);

                if (removed)
                    Console.WriteLine($"Removed client for issuer: {normalizedIssuer}");
                else
                    Console.WriteLine($"No registered client found for issuer: {normalizedIssuer}");
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, false);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static string GetSchemeType(SecurityScheme scheme)
    {
        if (scheme.SchemeCase == SecuritySchemeCase.HttpAuth)
        {
            var http = scheme.HttpAuthSecurityScheme!;
            return $"HTTP {http.Scheme ?? "Bearer"}";
        }
        else if (scheme.SchemeCase == SecuritySchemeCase.ApiKey)
        {
            var apiKey = scheme.ApiKeySecurityScheme!;
            return $"API Key (in {apiKey.Location}: {apiKey.Name})";
        }
        else if (scheme.SchemeCase == SecuritySchemeCase.OAuth2)
            return "OAuth 2.0";
        else if (scheme.SchemeCase == SecuritySchemeCase.OpenIdConnect)
            return "OpenID Connect";
        else if (scheme.SchemeCase == SecuritySchemeCase.Mtls)
            return "Mutual TLS";
        return "Unknown";
    }

    private static (string Name, OAuth2SecurityScheme Scheme)? FindOAuth2Scheme(AgentCard card)
    {
        if (card.SecuritySchemes == null) return null;

        foreach (var (name, scheme) in card.SecuritySchemes)
        {
            if (scheme.SchemeCase == SecuritySchemeCase.OAuth2)
            {
                var oauth2 = scheme.OAuth2SecurityScheme!;
                if (oauth2.Flows != null)
                    return (name, oauth2);
            }
        }

        return null;
    }

    private static IEnumerable<string>? ExtractRequiredScopes(AgentCard card, string schemeName)
    {
        if (card.SecurityRequirements == null) return null;

        var scopes = new List<string>();
        foreach (var req in card.SecurityRequirements)
        {
            if (req.Schemes != null && req.Schemes.TryGetValue(schemeName, out var scopeList))
                scopes.AddRange(scopeList.List);
        }
        return scopes.Count > 0 ? scopes : null;
    }

    internal static async Task<string?> ResolveIssuerAsync(
        OAuth2SecurityScheme scheme,
        CancellationToken cancellationToken = default)
    {
        string? discoveredIssuer = null;
        if (!string.IsNullOrWhiteSpace(scheme.OAuth2MetadataUrl))
        {
            var flow = new DeviceCodeFlow(scheme);
            var discovery = await flow.DiscoverEndpointsAsync(scheme.OAuth2MetadataUrl, cancellationToken);
            discoveredIssuer = discovery?.Issuer;
        }

        return ExtractIssuerFromOAuth2Scheme(scheme, discoveredIssuer);
    }

    internal static string? ExtractIssuerFromOAuth2Scheme(
        OAuth2SecurityScheme scheme,
        string? discoveredIssuer = null)
    {
        if (!string.IsNullOrWhiteSpace(discoveredIssuer))
            return ClientRegistrationStore.NormalizeIssuer(discoveredIssuer);

        if (!string.IsNullOrWhiteSpace(scheme.OAuth2MetadataUrl))
        {
            var metadataUri = new Uri(scheme.OAuth2MetadataUrl);
            var issuerPath = metadataUri.AbsolutePath;
            var wellKnownIndex = issuerPath.IndexOf("/.well-known/", StringComparison.OrdinalIgnoreCase);
            if (wellKnownIndex >= 0)
                issuerPath = issuerPath[..wellKnownIndex];

            var issuerUri = new UriBuilder(metadataUri)
            {
                Path = issuerPath,
                Query = string.Empty,
                Fragment = string.Empty
            };
            return ClientRegistrationStore.NormalizeIssuer(issuerUri.Uri.ToString());
        }

        var tokenUrl = GetTokenUrl(scheme);
        return string.IsNullOrWhiteSpace(tokenUrl)
            ? null
            : ClientRegistrationStore.NormalizeIssuer(new Uri(tokenUrl).GetLeftPart(UriPartial.Authority));
    }

    private static string? GetTokenUrl(OAuth2SecurityScheme scheme)
    {
        if (scheme.Flows == null)
            return null;

        return scheme.Flows.FlowCase switch
        {
            OAuthFlowCase.DeviceCode => scheme.Flows.DeviceCode?.TokenUrl,
            OAuthFlowCase.AuthorizationCode => scheme.Flows.AuthorizationCode?.TokenUrl,
            OAuthFlowCase.ClientCredentials => scheme.Flows.ClientCredentials?.TokenUrl,
            _ => null
        };
    }

    private static void WriteTable(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var widths = new int[headers.Count];
        for (var i = 0; i < headers.Count; i++)
            widths[i] = headers[i].Length;

        foreach (var row in rows)
        {
            for (var i = 0; i < row.Length; i++)
                widths[i] = Math.Max(widths[i], row[i].Length);
        }

        Console.WriteLine(FormatRow(headers, widths));
        Console.WriteLine(string.Join("  ", widths.Select(width => new string('-', width))));
        foreach (var row in rows)
            Console.WriteLine(FormatRow(row, widths));
    }

    private static string FormatRow(IReadOnlyList<string> cells, IReadOnlyList<int> widths) =>
        string.Join("  ", cells.Select((cell, index) => cell.PadRight(widths[index])));

    private static Command CreateLogoutCommand()
    {
        var urlArgument = new Argument<string>(
            name: "url",
            description: "Agent URL to remove stored token for");
        var tenantOption = CommonOptions.Tenant();

        var command = new Command("logout", "Remove stored authentication token for an agent")
        {
            urlArgument, tenantOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var tenant = context.ParseResult.GetValueForOption(tenantOption);
            try
            {
                var store = new TokenStore();
                var storageKey = TokenStore.BuildStorageKey(url, tenant);
                var token = await store.LoadTokenAsync(storageKey);
                if (token != null)
                {
                    await store.RemoveTokenAsync(storageKey);
                    Console.WriteLine($"Token removed for {url}");
                }
                else
                {
                    Console.WriteLine($"No stored token found for {url}");
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, false);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateStatusCommand()
    {
        var urlArgument = new Argument<string>(
            name: "url",
            description: "Agent URL to check authentication status for");
        var tenantOption = CommonOptions.Tenant();

        var command = new Command("status", "Show authentication status for an agent")
        {
            urlArgument, tenantOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var tenant = context.ParseResult.GetValueForOption(tenantOption);
            try
            {
                var store = new TokenStore();
                var storageKey = TokenStore.BuildStorageKey(url, tenant);
                var token = await store.LoadTokenAsync(storageKey);
                if (token == null)
                {
                    Console.WriteLine($"No stored token for {url}");
                    Console.WriteLine("Run: a2a-ask auth login <url>");
                    return;
                }

                Console.WriteLine($"Agent: {url}");
                Console.WriteLine($"Token type: {token.TokenType ?? "Bearer"}");
                if (token.ExpiresAt.HasValue)
                {
                    if (token.IsExpired)
                    {
                        Console.WriteLine($"Status: EXPIRED (expired {token.ExpiresAt.Value:u})");
                        if (!string.IsNullOrEmpty(token.RefreshToken))
                            Console.WriteLine("A refresh token is available — next command will attempt auto-refresh.");
                        else
                            Console.WriteLine("No refresh token. Run: a2a-ask auth login <url>");
                    }
                    else
                    {
                        var remaining = token.ExpiresAt.Value - DateTime.UtcNow;
                        Console.WriteLine($"Status: VALID (expires {token.ExpiresAt.Value:u}, {remaining.TotalMinutes:F0} min remaining)");
                    }
                }
                else
                {
                    Console.WriteLine("Status: VALID (no expiry set)");
                }

                Console.WriteLine($"Has refresh token: {!string.IsNullOrEmpty(token.RefreshToken)}");
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, false);
                context.ExitCode = 1;
            }
        });

        return command;
    }
}
