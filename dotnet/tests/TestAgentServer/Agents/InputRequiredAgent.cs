using A2A;

namespace TestAgentServer.Agents;

/// <summary>
/// Agent that demonstrates the input-required task state.
/// First message: transitions to input-required asking for a name.
/// Continuation: completes with a greeting.
/// </summary>
public class InputRequiredAgent : IAgentHandler
{
    public static AgentCard GetAgentCard() => new()
    {
        Name = "InputRequiredAgent",
        Description = "An agent that requires additional input before completing",
        Version = "1.0.0",
    };

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var updater = new TaskUpdater(eventQueue, context.TaskId, context.ContextId);
        await updater.SubmitAsync(cancellationToken);

        if (!context.IsContinuation)
        {
            await updater.RequireInputAsync(new Message
            {
                Role = Role.Agent,
                Parts = [Part.FromText("What is your name?")],
            }, cancellationToken);
        }
        else
        {
            await updater.CompleteAsync(new Message
            {
                Role = Role.Agent,
                Parts = [Part.FromText($"Hello, {context.UserText}!")],
            }, cancellationToken);
        }
    }
}
