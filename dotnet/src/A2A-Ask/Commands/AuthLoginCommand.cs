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
                Console.WriteLine($"Security schemes available:");
                foreach (var (name, scheme) in card.SecuritySchemes)
                {
                    var schemeType = GetSchemeType(scheme);
                    Console.WriteLine($"  {name}: {schemeType}");
                }
                Console.WriteLine();

                var storageKey = TokenStore.BuildStorageKey(url, tenant);

                // If client_id and client_secret provided, use client credentials flow
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
                        oauth2Scheme.Value.Scheme, clientId, clientSecret,
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

                    TokenResult? tokenResult;

                    // Pick flow based on what's available: device code preferred, then auth code
                    if (scheme.Flows?.DeviceCode != null)
                    {
                        Console.WriteLine($"Using OAuth2 device code flow via '{schemeName}'...");
                        Console.WriteLine();
                        var flow = new DeviceCodeFlow(scheme);
                        tokenResult = await flow.AuthenticateAsync(
                            requiredScopes, context.GetCancellationToken());
                    }
                    else if (scheme.Flows?.AuthorizationCode != null)
                    {
                        Console.WriteLine($"Using OAuth2 authorization code flow via '{schemeName}'...");
                        Console.WriteLine("Opening browser for authentication...");
                        Console.WriteLine();
                        tokenResult = await AuthCodeFlow.AuthenticateAsync(
                            scheme, requiredScopes, context.GetCancellationToken());
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

                    foreach (var (name, scheme) in card.SecuritySchemes)
                    {
                        if (scheme.SchemeCase == SecuritySchemeCase.HttpAuth)
                        {
                            var http = scheme.HttpAuthSecurityScheme!;
                            if (string.Equals(http.Scheme, "basic", StringComparison.OrdinalIgnoreCase))
                                Console.WriteLine($"  --auth-user <username> --auth-password <password>");
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
                            Console.WriteLine($"  --auth-token <token-from-oidc-provider>");
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

    private static (string Name, OAuth2SecurityScheme Scheme)?
        FindOAuth2Scheme(AgentCard card)
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
            {
                scopes.AddRange(scopeList.List);
            }
        }
        return scopes.Count > 0 ? scopes : null;
    }

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
