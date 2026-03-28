using System.Text;
using System.Text.Json;

namespace TestAgentServer.Middleware;

public class TestAuthMiddleware
{
    private readonly RequestDelegate _next;

    public TestAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Agent card discovery is always public (no auth required)
        if (path.Contains("/.well-known/agent-card.json"))
        {
            await _next(context);
            return;
        }

        if (path.StartsWith("/open") || path.StartsWith("/input-required") || path.StartsWith("/auth-required"))
        {
            await _next(context);
            return;
        }

        if (path.StartsWith("/api-key-header"))
        {
            if (context.Request.Headers["X-Api-Key"] != "test-api-key-123")
            {
                await WriteUnauthorized(context, "apikey-header");
                return;
            }
        }
        else if (path.StartsWith("/api-key-cookie"))
        {
            if (!context.Request.Cookies.TryGetValue("api_key", out var cookie) || cookie != "test-cookie-key-456")
            {
                await WriteUnauthorized(context, "apikey-cookie");
                return;
            }
        }
        else if (path.StartsWith("/bearer"))
        {
            if (!ValidateBearer(context, "test-bearer-token-789"))
            {
                await WriteUnauthorized(context, "bearer");
                return;
            }
        }
        else if (path.StartsWith("/basic"))
        {
            if (!ValidateBasic(context, "testuser", "testpass"))
            {
                await WriteUnauthorized(context, "basic");
                return;
            }
        }
        else if (path.StartsWith("/oauth2-static"))
        {
            if (!ValidateBearer(context, "test-oauth2-token-abc"))
            {
                await WriteUnauthorized(context, "oauth2");
                return;
            }
        }
        else if (path.StartsWith("/multi-auth"))
        {
            var hasApiKey = context.Request.Headers["X-Api-Key"] == "test-api-key-123";
            var hasBearer = ValidateBearer(context, "test-bearer-token-789");
            if (!hasApiKey && !hasBearer)
            {
                await WriteUnauthorized(context, "multi");
                return;
            }
        }
        else if (path.StartsWith("/tenant"))
        {
            var tenant = context.Request.Headers["X-Tenant"].ToString();
            var expectedToken = tenant switch
            {
                "tenant-a" => "tenant-a-token",
                "tenant-b" => "tenant-b-token",
                _ => (string?)null
            };
            if (expectedToken is null || !ValidateBearer(context, expectedToken))
            {
                await WriteUnauthorized(context, "tenant-bearer");
                return;
            }
        }

        await _next(context);
    }

    private static bool ValidateBearer(HttpContext context, string expectedToken)
    {
        var auth = context.Request.Headers.Authorization.ToString();
        return auth == $"Bearer {expectedToken}";
    }

    private static bool ValidateBasic(HttpContext context, string username, string password)
    {
        var auth = context.Request.Headers.Authorization.ToString();
        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return auth == $"Basic {expected}";
    }

    private static async Task WriteUnauthorized(HttpContext context, string scheme)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";
        var body = JsonSerializer.Serialize(new { error = "unauthorized", scheme });
        await context.Response.WriteAsync(body);
    }
}
