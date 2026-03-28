using System.Net;
using System.Net.Http.Headers;
using System.Text;
using A2A;
using Xunit;

namespace A2AAsk.IntegrationTests;

/// <summary>
/// Tests sending messages to agents with various auth schemes.
/// Verifies correct credentials succeed and missing/wrong credentials fail.
/// </summary>
[Collection("TestServer")]
public class SendTests
{
    private readonly TestServerFixture _fixture;

    public SendTests(TestServerFixture fixture) => _fixture = fixture;

    private A2AClient CreateClient(string path, HttpClient? httpClient = null)
    {
        var client = httpClient ?? _fixture.Client;
        var uri = new Uri(client.BaseAddress!, path);
        return new A2AClient(uri, client);
    }

    private static SendMessageRequest MakeRequest(string text) => new()
    {
        Message = new Message
        {
            Role = Role.User,
            MessageId = Guid.NewGuid().ToString("N"),
            Parts = [Part.FromText(text)],
        },
        Configuration = new SendMessageConfiguration { Blocking = true },
    };

    private static string? GetResponseText(SendMessageResponse response)
    {
        if (response.Message is { } msg)
            return msg.Parts.FirstOrDefault(p => p.Text is not null)?.Text;
        if (response.Task is { } task)
            return task.Status?.Message?.Parts?.FirstOrDefault(p => p.Text is not null)?.Text;
        return null;
    }

    // ── Open Agent ──────────────────────────────────────────────────

    [Fact]
    public async Task Send_OpenAgent_NoAuth_Succeeds()
    {
        var client = CreateClient("/open");
        var response = await client.SendMessageAsync(MakeRequest("hello"));

        Assert.NotNull(response);
        var text = GetResponseText(response);
        Assert.Contains("Echo", text);
        Assert.Contains("hello", text);
    }

    // ── API Key Header Agent ────────────────────────────────────────

    [Fact]
    public async Task Send_ApiKeyHeaderAgent_WithKey_Succeeds()
    {
        var httpClient = _fixture.CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key-123");
        var client = CreateClient("/api-key-header", httpClient);

        var response = await client.SendMessageAsync(MakeRequest("hello"));

        Assert.NotNull(response);
        var text = GetResponseText(response);
        Assert.Contains("Echo", text);
    }

    [Fact]
    public async Task Send_ApiKeyHeaderAgent_WithoutKey_Returns401()
    {
        var httpClient = _fixture.CreateClient();
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var client = CreateClient("/api-key-header", httpClient);
            await client.SendMessageAsync(MakeRequest("hello"));
        });
        // The JSON-RPC client should throw on 401
        Assert.True(ex is HttpRequestException || ex is InvalidOperationException,
            $"Expected HttpRequestException or InvalidOperationException, got {ex.GetType().Name}: {ex.Message}");
    }

    [Fact]
    public async Task Send_ApiKeyHeaderAgent_WrongKey_Returns401()
    {
        var httpClient = _fixture.CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", "wrong-key");
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var client = CreateClient("/api-key-header", httpClient);
            await client.SendMessageAsync(MakeRequest("hello"));
        });
        Assert.True(ex is HttpRequestException || ex is InvalidOperationException,
            $"Expected auth failure, got {ex.GetType().Name}: {ex.Message}");
    }

    // ── API Key Cookie Agent ────────────────────────────────────────

    [Fact]
    public async Task Send_ApiKeyCookieAgent_WithCookie_Succeeds()
    {
        var handler = new CookieHandler("api_key", "test-cookie-key-456");
        var httpClient = _fixture.CreateClientWithHandler(handler);
        var client = CreateClient("/api-key-cookie", httpClient);

        var response = await client.SendMessageAsync(MakeRequest("hello"));

        Assert.NotNull(response);
        var text = GetResponseText(response);
        Assert.Contains("Echo", text);
    }

    [Fact]
    public async Task Send_ApiKeyCookieAgent_WithoutCookie_Returns401()
    {
        var httpClient = _fixture.CreateClient();
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var client = CreateClient("/api-key-cookie", httpClient);
            await client.SendMessageAsync(MakeRequest("hello"));
        });
        Assert.True(ex is HttpRequestException || ex is InvalidOperationException,
            $"Expected auth failure, got {ex.GetType().Name}: {ex.Message}");
    }

    // ── Bearer Agent ────────────────────────────────────────────────

    [Fact]
    public async Task Send_BearerAgent_WithToken_Succeeds()
    {
        var httpClient = _fixture.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-bearer-token-789");
        var client = CreateClient("/bearer", httpClient);

        var response = await client.SendMessageAsync(MakeRequest("hello"));

        Assert.NotNull(response);
        var text = GetResponseText(response);
        Assert.Contains("Echo", text);
    }

    [Fact]
    public async Task Send_BearerAgent_WithoutToken_Returns401()
    {
        var httpClient = _fixture.CreateClient();
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var client = CreateClient("/bearer", httpClient);
            await client.SendMessageAsync(MakeRequest("hello"));
        });
        Assert.True(ex is HttpRequestException || ex is InvalidOperationException,
            $"Expected auth failure, got {ex.GetType().Name}: {ex.Message}");
    }

    // ── Basic Auth Agent ────────────────────────────────────────────

    [Fact]
    public async Task Send_BasicAgent_WithCredentials_Succeeds()
    {
        var httpClient = _fixture.CreateClient();
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes("testuser:testpass"));
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", creds);
        var client = CreateClient("/basic", httpClient);

        var response = await client.SendMessageAsync(MakeRequest("hello"));

        Assert.NotNull(response);
        var text = GetResponseText(response);
        Assert.Contains("Echo", text);
    }

    [Fact]
    public async Task Send_BasicAgent_WrongCredentials_Returns401()
    {
        var httpClient = _fixture.CreateClient();
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes("wrong:creds"));
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", creds);
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var client = CreateClient("/basic", httpClient);
            await client.SendMessageAsync(MakeRequest("hello"));
        });
        Assert.True(ex is HttpRequestException || ex is InvalidOperationException,
            $"Expected auth failure, got {ex.GetType().Name}: {ex.Message}");
    }

    // ── OAuth2 Static Agent ─────────────────────────────────────────

    [Fact]
    public async Task Send_OAuth2StaticAgent_WithToken_Succeeds()
    {
        var httpClient = _fixture.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-oauth2-token-abc");
        var client = CreateClient("/oauth2-static", httpClient);

        var response = await client.SendMessageAsync(MakeRequest("hello"));

        Assert.NotNull(response);
        var text = GetResponseText(response);
        Assert.Contains("Echo", text);
    }

    // ── Multi Auth Agent ────────────────────────────────────────────

    [Fact]
    public async Task Send_MultiAuthAgent_WithApiKey_Succeeds()
    {
        var httpClient = _fixture.CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Api-Key", "test-api-key-123");
        var client = CreateClient("/multi-auth", httpClient);

        var response = await client.SendMessageAsync(MakeRequest("hello"));

        Assert.NotNull(response);
        var text = GetResponseText(response);
        Assert.Contains("Echo", text);
    }

    [Fact]
    public async Task Send_MultiAuthAgent_WithBearer_Succeeds()
    {
        var httpClient = _fixture.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "test-bearer-token-789");
        var client = CreateClient("/multi-auth", httpClient);

        var response = await client.SendMessageAsync(MakeRequest("hello"));

        Assert.NotNull(response);
        var text = GetResponseText(response);
        Assert.Contains("Echo", text);
    }

    [Fact]
    public async Task Send_MultiAuthAgent_WithoutAuth_Returns401()
    {
        var httpClient = _fixture.CreateClient();
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var client = CreateClient("/multi-auth", httpClient);
            await client.SendMessageAsync(MakeRequest("hello"));
        });
        Assert.True(ex is HttpRequestException || ex is InvalidOperationException,
            $"Expected auth failure, got {ex.GetType().Name}: {ex.Message}");
    }

    // ── Tenant Agent ────────────────────────────────────────────────

    [Fact]
    public async Task Send_TenantAgent_TenantA_Succeeds()
    {
        var httpClient = _fixture.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "tenant-a-token");
        httpClient.DefaultRequestHeaders.Add("X-Tenant", "tenant-a");
        var client = CreateClient("/tenant", httpClient);

        var response = await client.SendMessageAsync(MakeRequest("hello"));

        var text = GetResponseText(response);
        Assert.Contains("tenant=tenant-a", text);
        Assert.Contains("hello", text);
    }

    [Fact]
    public async Task Send_TenantAgent_TenantB_Succeeds()
    {
        var httpClient = _fixture.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "tenant-b-token");
        httpClient.DefaultRequestHeaders.Add("X-Tenant", "tenant-b");
        var client = CreateClient("/tenant", httpClient);

        var response = await client.SendMessageAsync(MakeRequest("hello"));

        var text = GetResponseText(response);
        Assert.Contains("tenant=tenant-b", text);
    }

    [Fact]
    public async Task Send_TenantAgent_WrongTokenForTenant_Returns401()
    {
        var httpClient = _fixture.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "tenant-a-token");
        httpClient.DefaultRequestHeaders.Add("X-Tenant", "tenant-b"); // wrong combo
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            var client = CreateClient("/tenant", httpClient);
            await client.SendMessageAsync(MakeRequest("hello"));
        });
        Assert.True(ex is HttpRequestException || ex is InvalidOperationException,
            $"Expected auth failure, got {ex.GetType().Name}: {ex.Message}");
    }

    // ── Helper: Cookie handler for in-process test server ───────────

    private class CookieHandler : DelegatingHandler
    {
        private readonly string _name;
        private readonly string _value;

        public CookieHandler(string name, string value)
        {
            _name = name;
            _value = value;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("Cookie", $"{_name}={_value}");
            return base.SendAsync(request, cancellationToken);
        }
    }
}
