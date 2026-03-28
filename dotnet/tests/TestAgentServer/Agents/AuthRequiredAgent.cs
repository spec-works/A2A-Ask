using A2A;

namespace TestAgentServer.Agents;

/// <summary>
/// Agent that demonstrates the auth-required task state.
/// Without Bearer token: transitions to auth-required.
/// With Bearer token: echoes the input.
/// </summary>
public class AuthRequiredAgent : IAgentHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthRequiredAgent(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public static AgentCard GetAgentCard() => new()
    {
        Name = "AuthRequiredAgent",
        Description = "An agent that requests authentication via the A2A protocol",
        Version = "1.0.0",
    };

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var auth = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        var hasBearer = auth?.StartsWith("Bearer ") == true;

        if (!hasBearer)
        {
            var updater = new TaskUpdater(eventQueue, context.TaskId, context.ContextId);
            await updater.SubmitAsync(cancellationToken);
            await updater.RequireAuthAsync(new Message
            {
                Role = Role.Agent,
                Parts = [Part.FromText("Authentication required")],
            }, cancellationToken);
        }
        else
        {
            var responder = new MessageResponder(eventQueue, context.ContextId);
            await responder.ReplyAsync($"Echo: {context.UserText}", cancellationToken);
        }
    }
}
