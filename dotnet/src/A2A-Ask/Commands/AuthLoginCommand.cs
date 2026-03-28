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

        var command = new Command("login", "Interactively authenticate with an A2A agent")
        {
            urlArgument
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
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

                var oauth2Scheme = FindOAuth2Scheme(card);
                if (oauth2Scheme.HasValue)
                {
                    var (schemeName, oauth2) = oauth2Scheme.Value;
                    Console.WriteLine($"Using OAuth2 flow via '{schemeName}'...");
                    Console.WriteLine();

                    var flow = new DeviceCodeFlow(oauth2);
                    var tokenResult = await flow.AuthenticateAsync(context.GetCancellationToken());

                    if (tokenResult != null)
                    {
                        var store = new TokenStore();
                        await store.SaveTokenAsync(url, tokenResult);
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

    private static Command CreateLogoutCommand()
    {
        var urlArgument = new Argument<string>(
            name: "url",
            description: "Agent URL to remove stored token for");

        var command = new Command("logout", "Remove stored authentication token for an agent")
        {
            urlArgument
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            try
            {
                var store = new TokenStore();
                var token = await store.LoadTokenAsync(url);
                if (token != null)
                {
                    await store.RemoveTokenAsync(url);
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

        var command = new Command("status", "Show authentication status for an agent")
        {
            urlArgument
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            try
            {
                var store = new TokenStore();
                var token = await store.LoadTokenAsync(url);
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
