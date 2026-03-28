using A2A;

namespace TestAgentServer.Agents;

/// <summary>
/// Echo agent secured with OAuth2 (uses a static token for testing).
/// Auth is validated by middleware; agent just echoes.
/// </summary>
public class OAuth2StaticAgent : IAgentHandler
{
    public const string ExpectedToken = "test-oauth2-token-abc";

    public static AgentCard GetAgentCard() => new()
    {
        Name = "OAuth2StaticAgent",
        Description = "An echo agent secured with OAuth2 (static token for testing)",
        Version = "1.0.0",
        SecuritySchemes = new Dictionary<string, SecurityScheme>
        {
            ["oauth2"] = new SecurityScheme
            {
                OAuth2SecurityScheme = new OAuth2SecurityScheme
                {
                    Flows = new OAuthFlows
                    {
                        ClientCredentials = new ClientCredentialsOAuthFlow
                        {
                            TokenUrl = "https://auth.example.com/token",
                            Scopes = new Dictionary<string, string>
                            {
                                ["read"] = "Read access",
                            },
                        },
                    },
                }
            }
        },
        SecurityRequirements =
        [
            new SecurityRequirement
            {
                Schemes = new Dictionary<string, StringList>
                {
                    ["oauth2"] = new StringList { List = ["read"] }
                }
            }
        ],
    };

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var responder = new MessageResponder(eventQueue, context.ContextId);
        await responder.ReplyAsync($"Echo: {context.UserText}", cancellationToken);
    }
}
