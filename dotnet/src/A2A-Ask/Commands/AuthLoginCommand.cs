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
}
