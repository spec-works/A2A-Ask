using System.Text.Json;
using A2A;
using A2A.AspNetCore;
using TestAgentServer.Agents;
using TestAgentServer.Middleware;
using V03 = A2A.V0_3;

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

app.MapGet("/direct-only/.well-known/agent-card.json", () =>
{
    TestAgentServer.RequestCaptureState.RecordDirectCardFetch();
    return Results.NotFound();
});

app.MapPost("/direct-only", async (HttpRequest request, CancellationToken cancellationToken) =>
{
    var rpcRequest = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(request.Body, cancellationToken: cancellationToken);
    if (rpcRequest is null)
    {
        return Results.BadRequest();
    }

    TestAgentServer.RequestCaptureState.RecordDirectRequest(rpcRequest.Method, request.Headers["A2A-Version"].FirstOrDefault());

    if (!string.Equals(rpcRequest.Method, A2AMethods.SendMessage, StringComparison.Ordinal))
    {
        return Results.Json(JsonRpcResponse.MethodNotFoundResponse(rpcRequest.Id, $"Unexpected method '{rpcRequest.Method}'."));
    }

    return Results.Json(JsonRpcResponse.CreateJsonRpcResponse(
        rpcRequest.Id,
        new SendMessageResponse
        {
            Message = new Message
            {
                Role = Role.Agent,
                MessageId = Guid.NewGuid().ToString("N"),
                Parts = [Part.FromText("Direct send ok")]
            }
        }));
});

app.MapGet("/v03-direct/.well-known/agent-card.json", () =>
{
    TestAgentServer.RequestCaptureState.RecordV03CardFetch();
    return Results.NotFound();
});

app.MapPost("/v03-direct", async (HttpRequest request, CancellationToken cancellationToken) =>
{
    using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
    var root = document.RootElement;
    var method = root.GetProperty("method").GetString() ?? string.Empty;
    TestAgentServer.RequestCaptureState.RecordV03Request(method, request.Headers["A2A-Version"].FirstOrDefault());

    var idElement = root.GetProperty("id");
    var rpcId = idElement.ValueKind == JsonValueKind.Number
        ? new V03.JsonRpcId(idElement.GetInt64())
        : new V03.JsonRpcId(idElement.GetString());

    if (string.Equals(method, V03.A2AMethods.MessageStream, StringComparison.Ordinal))
    {
        var streamResponse = V03.JsonRpcResponse.CreateJsonRpcResponse(
            rpcId,
            new V03.AgentMessage
            {
                Role = V03.MessageRole.Agent,
                MessageId = Guid.NewGuid().ToString("N"),
                Parts = [new V03.TextPart { Text = "v0.3 stream ok" }]
            });

        return Results.Text($"data: {JsonSerializer.Serialize(streamResponse)}\n\n", "text/event-stream");
    }

    object response = method switch
    {
        var value when string.Equals(value, V03.A2AMethods.MessageSend, StringComparison.Ordinal) =>
            V03.JsonRpcResponse.CreateJsonRpcResponse(
                rpcId,
                new V03.AgentMessage
                {
                    Role = V03.MessageRole.Agent,
                    MessageId = Guid.NewGuid().ToString("N"),
                    Parts = [new V03.TextPart { Text = "v0.3 send ok" }]
                }),
        var value when string.Equals(value, V03.A2AMethods.TaskGet, StringComparison.Ordinal)
            || string.Equals(value, V03.A2AMethods.TaskCancel, StringComparison.Ordinal) =>
            V03.JsonRpcResponse.CreateJsonRpcResponse(
                rpcId,
                new V03.AgentTask
                {
                    Id = "task-123",
                    ContextId = "ctx-123",
                    Status = new V03.AgentTaskStatus
                    {
                        State = V03.TaskState.Completed,
                        Message = new V03.AgentMessage
                        {
                            Role = V03.MessageRole.Agent,
                            MessageId = Guid.NewGuid().ToString("N"),
                            Parts = [new V03.TextPart { Text = "v0.3 task ok" }]
                        }
                    }
                }),
        _ => V03.JsonRpcResponse.MethodNotFoundResponse(rpcId, $"Unexpected method '{method}'.")
    };

    return Results.Json(response);
});

app.MapGet("/.well-known/ai-catalog.json", () => Results.Json(new
{
    specVersion = "1.0",
    entries = new[]
    {
        new
        {
            identifier = "open",
            displayName = "OpenAgent",
            mediaType = "application/a2a-agent-card+json",
            url = "/open/.well-known/agent-card.json",
            description = "Open echo agent",
            tags = new[] { "echo", "open" }
        }
    }
}));

app.MapGet("/catalogs/multi/.well-known/ai-catalog.json", () => Results.Json(new
{
    specVersion = "1.0",
    entries = new object[]
    {
        new
        {
            identifier = "open",
            displayName = "OpenAgent",
            mediaType = "application/a2a-agent-card+json",
            url = "/open/.well-known/agent-card.json",
            description = "Open echo agent",
            tags = new[] { "echo", "general" }
        },
        new
        {
            identifier = "input-required",
            displayName = "InputRequiredAgent",
            mediaType = "application/vnd.a2a.agent-card+json",
            url = "/input-required/.well-known/agent-card.json",
            description = "Requires follow-up input",
            tags = new[] { "input", "workflow" }
        }
    }
}));

app.MapGet("/catalogs/relative/.well-known/ai-catalog.json", () => Results.Json(new
{
    specVersion = "1.0",
    entries = new[]
    {
        new
        {
            identifier = "relative-open",
            displayName = "OpenAgent",
            mediaType = "application/a2a-agent-card+json",
            url = "../../../open/.well-known/agent-card.json",
            description = "Relative URL to the open agent",
            tags = new[] { "echo", "relative" }
        }
    }
}));

app.Run();

// Make Program accessible for WebApplicationFactory<TestAgentServer.Program> in integration tests
namespace TestAgentServer
{
    public partial class Program { }
}
