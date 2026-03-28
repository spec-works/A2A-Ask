using A2A;

namespace TestAgentServer.Agents;

/// <summary>
/// Echo agent that accepts either API key header OR Bearer token.
/// Auth is validated by middleware; agent just echoes.
/// </summary>
public class MultiAuthAgent : IAgentHandler
{
    public static AgentCard GetAgentCard() => new()
    {
        Name = "MultiAuthAgent",
        Description = "An echo agent that accepts either API key or Bearer authentication",
        Version = "1.0.0",
        SecuritySchemes = new Dictionary<string, SecurityScheme>
        {
            ["api_key"] = new SecurityScheme
            {
                ApiKeySecurityScheme = new ApiKeySecurityScheme
                {
                    Name = "X-Api-Key",
                    Location = "header",
                }
            },
            ["bearer"] = new SecurityScheme
            {
                HttpAuthSecurityScheme = new HttpAuthSecurityScheme
                {
                    Scheme = "bearer",
                }
            },
        },
        // Two separate entries = either is sufficient (OR logic)
        SecurityRequirements =
        [
            new SecurityRequirement
            {
                Schemes = new Dictionary<string, StringList>
                {
                    ["api_key"] = new StringList()
                }
            },
            new SecurityRequirement
            {
                Schemes = new Dictionary<string, StringList>
                {
                    ["bearer"] = new StringList()
                }
            },
        ],
    };

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var responder = new MessageResponder(eventQueue, context.ContextId);
        await responder.ReplyAsync($"Echo: {context.UserText}", cancellationToken);
    }
}
