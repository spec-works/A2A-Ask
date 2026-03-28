using A2A;

namespace TestAgentServer.Agents;

/// <summary>
/// Echo agent requiring API key in a cookie.
/// Auth is validated by middleware; agent just echoes.
/// </summary>
public class ApiKeyCookieAgent : IAgentHandler
{
    public const string ExpectedApiKey = "test-cookie-key-456";
    public const string CookieName = "api_key";

    public static AgentCard GetAgentCard() => new()
    {
        Name = "ApiKeyCookieAgent",
        Description = "An echo agent secured with an API key in a cookie",
        Version = "1.0.0",
        SecuritySchemes = new Dictionary<string, SecurityScheme>
        {
            ["apikey-cookie"] = new SecurityScheme
            {
                ApiKeySecurityScheme = new ApiKeySecurityScheme
                {
                    Name = CookieName,
                    Location = "cookie",
                    Description = "API key passed in a cookie",
                }
            }
        },
        SecurityRequirements =
        [
            new SecurityRequirement
            {
                Schemes = new Dictionary<string, StringList>
                {
                    ["apikey-cookie"] = new StringList()
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
