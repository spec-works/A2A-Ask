using A2A;

namespace TestAgentServer.Agents;

public class ApiKeyHeaderAgent : IAgentHandler
{
    public const string ExpectedApiKey = "test-api-key-123";
    public const string HeaderName = "X-Api-Key";

    public static AgentCard GetAgentCard() => new()
    {
        Name = "ApiKeyHeaderAgent",
        Description = "An echo agent secured with an API key in X-Api-Key header",
        Version = "1.0.0",
        SecuritySchemes = new Dictionary<string, SecurityScheme>
        {
            ["apikey-header"] = new SecurityScheme
            {
                ApiKeySecurityScheme = new ApiKeySecurityScheme
                {
                    Name = HeaderName,
                    Location = "header",
                    Description = "API key passed in X-Api-Key header",
                }
            }
        },
        SecurityRequirements =
        [
            new SecurityRequirement
            {
                Schemes = new Dictionary<string, StringList>
                {
                    ["apikey-header"] = new StringList()
                }
            }
        ],
    };

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken ct)
    {
        var responder = new MessageResponder(eventQueue, context.ContextId);
        await responder.ReplyAsync($"Echo: {context.UserText}", ct);
    }
}
