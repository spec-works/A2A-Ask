using A2A;

namespace TestAgentServer.Agents;

/// <summary>
/// Echo agent requiring HTTP Basic authentication.
/// Auth is validated by middleware; agent just echoes.
/// </summary>
public class BasicAuthAgent : IAgentHandler
{
    public const string Username = "testuser";
    public const string Password = "testpass";

    public static AgentCard GetAgentCard() => new()
    {
        Name = "BasicAuthAgent",
        Description = "An echo agent secured with HTTP Basic authentication",
        Version = "1.0.0",
        SecuritySchemes = new Dictionary<string, SecurityScheme>
        {
            ["basic"] = new SecurityScheme
            {
                HttpAuthSecurityScheme = new HttpAuthSecurityScheme
                {
                    Scheme = "basic",
                }
            }
        },
        SecurityRequirements =
        [
            new SecurityRequirement
            {
                Schemes = new Dictionary<string, StringList>
                {
                    ["basic"] = new StringList()
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
