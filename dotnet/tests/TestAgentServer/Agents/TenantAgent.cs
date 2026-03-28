using A2A;

namespace TestAgentServer.Agents;

/// <summary>
/// Echo agent that includes tenant information in the response.
/// Auth is validated by middleware (Bearer per-tenant + X-Tenant header).
/// </summary>
public class TenantAgent : IAgentHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantAgent(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public static AgentCard GetAgentCard() => new()
    {
        Name = "TenantAgent",
        Description = "An echo agent that includes tenant information, secured with Bearer per-tenant",
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
        var tenant = _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant"].ToString() ?? "unknown";
        var responder = new MessageResponder(eventQueue, context.ContextId);
        await responder.ReplyAsync($"Echo [tenant={tenant}]: {context.UserText}", cancellationToken);
    }
}
