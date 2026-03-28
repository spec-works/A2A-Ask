using A2A;
using A2A.AspNetCore;
using TestAgentServer.Agents;
using TestAgentServer.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();

var app = builder.Build();
app.UseMiddleware<TestAuthMiddleware>();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<A2AServer>();
var httpContextAccessor = app.Services.GetRequiredService<IHttpContextAccessor>();

A2AServer CreateServer(IAgentHandler handler) =>
    new(handler, new InMemoryTaskStore(), new ChannelEventNotifier(), logger);

void MapAgent(string path, IAgentHandler handler, AgentCard card)
{
    app.MapA2A(CreateServer(handler), path);
    app.MapWellKnownAgentCard(card, path);
}

MapAgent("/open", new OpenAgent(), OpenAgent.GetAgentCard());
MapAgent("/api-key-header", new ApiKeyHeaderAgent(), ApiKeyHeaderAgent.GetAgentCard());
MapAgent("/api-key-cookie", new ApiKeyCookieAgent(), ApiKeyCookieAgent.GetAgentCard());
MapAgent("/bearer", new BearerAgent(), BearerAgent.GetAgentCard());
MapAgent("/basic", new BasicAuthAgent(), BasicAuthAgent.GetAgentCard());
MapAgent("/oauth2-static", new OAuth2StaticAgent(), OAuth2StaticAgent.GetAgentCard());
MapAgent("/multi-auth", new MultiAuthAgent(), MultiAuthAgent.GetAgentCard());
MapAgent("/tenant", new TenantAgent(httpContextAccessor), TenantAgent.GetAgentCard());
MapAgent("/input-required", new InputRequiredAgent(), InputRequiredAgent.GetAgentCard());
MapAgent("/auth-required", new AuthRequiredAgent(httpContextAccessor), AuthRequiredAgent.GetAgentCard());

app.Run();

// Make Program accessible for WebApplicationFactory<Program> in integration tests
public partial class Program { }
