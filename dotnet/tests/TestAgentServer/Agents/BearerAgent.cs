using A2A;

namespace TestAgentServer.Agents;

/// <summary>
/// Echo agent requiring HTTP Bearer token authentication.
/// Auth is validated by middleware; agent just echoes.
/// </summary>
public class BearerAgent : IAgentHandler
{
    public const string ExpectedToken = "test-bearer-token-789";

    public static AgentCard GetAgentCard() => new()
    {
        Name = "BearerAgent",
        Description = "An echo agent secured with HTTP Bearer authentication",
        Version = "1.0.0",
        SecuritySchemes = new Dictionary<string, SecurityScheme>
        {
            ["bearer"] = new SecurityScheme
            {
                HttpAuthSecurityScheme = new HttpAuthSecurityScheme
                {
                    Scheme = "bearer",
                }
            }
        },
        SecurityRequirements =
        [
            new SecurityRequirement
            {
                Schemes = new Dictionary<string, StringList>
                {
                    ["bearer"] = new StringList()
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
