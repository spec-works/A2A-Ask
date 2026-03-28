using A2A;

namespace TestAgentServer.Agents;

public class OpenAgent : IAgentHandler
{
    public static AgentCard GetAgentCard() => new()
    {
        Name = "OpenAgent",
        Description = "An open echo agent with no authentication",
        Version = "1.0.0",
        Capabilities = new AgentCapabilities { Streaming = true },
    };

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken ct)
    {
        var responder = new MessageResponder(eventQueue, context.ContextId);
        await responder.ReplyAsync($"Echo: {context.UserText}", ct);
    }
}
