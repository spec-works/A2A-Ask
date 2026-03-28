using System.Net.Http.Headers;
using A2A;
using A2AAsk.Auth;
using Xunit;

namespace A2AAsk.IntegrationTests;

[Collection("TestServer")]
public class TokenStoreTests
{
    private readonly TestServerFixture _fixture;

    public TokenStoreTests(TestServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveAndLoad_RoundTrip()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"token-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var storePath = Path.Combine(tempDir, "tokens.json");
            var store = new TokenStore(storePath, useEncryption: false);

            var key = TokenStore.BuildStorageKey("https://example.com/agent");
            var token = new TokenResult
            {
                AccessToken = "my-access-token",
                RefreshToken = "my-refresh-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                TokenUrl = "https://example.com/oauth/token"
            };

            await store.SaveTokenAsync(key, token);
            var loaded = await store.LoadTokenAsync(key);

            Assert.NotNull(loaded);
            Assert.Equal(token.AccessToken, loaded.AccessToken);
            Assert.Equal(token.RefreshToken, loaded.RefreshToken);
            Assert.Equal(token.TokenUrl, loaded.TokenUrl);
            Assert.NotNull(loaded.ExpiresAt);
            Assert.InRange(
                (loaded.ExpiresAt!.Value - token.ExpiresAt!.Value).TotalSeconds,
                -1, 1);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SavedToken_UsedInRequest()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"token-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var storePath = Path.Combine(tempDir, "tokens.json");
            var store = new TokenStore(storePath, useEncryption: false);

            var agentUrl = $"{_fixture.BaseAddress}/bearer";
            var key = TokenStore.BuildStorageKey(agentUrl);
            var token = new TokenResult
            {
                AccessToken = "test-bearer-token-789",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
            };

            await store.SaveTokenAsync(key, token);

            // Load the token back from the store (simulating auto-load)
            var loaded = await store.LoadTokenAsync(key);
            Assert.NotNull(loaded);

            // Create a client and attach the stored token as Authorization header
            var httpClient = _fixture.CreateClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loaded.AccessToken);

            var uri = new Uri(httpClient.BaseAddress!, "/bearer");
            var a2aClient = new A2AClient(uri, httpClient);
            var request = new SendMessageRequest
            {
                Message = new Message
                {
                    Role = Role.User,
                    MessageId = Guid.NewGuid().ToString("N"),
                    Parts = [Part.FromText("hello from token store")],
                },
                Configuration = new SendMessageConfiguration { Blocking = true },
            };

            var response = await a2aClient.SendMessageAsync(request);

            Assert.NotNull(response);
            var text = response.Message?.Parts?.FirstOrDefault(p => p.Text is not null)?.Text
                    ?? response.Task?.Status?.Message?.Parts?.FirstOrDefault(p => p.Text is not null)?.Text;
            Assert.NotNull(text);
            Assert.Contains("hello from token store", text);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task TenantIsolation()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"token-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var storePath = Path.Combine(tempDir, "tokens.json");
            var store = new TokenStore(storePath, useEncryption: false);

            var agentUrl = $"{_fixture.BaseAddress}/tenant";
            var keyA = TokenStore.BuildStorageKey(agentUrl, tenant: "tenant-a");
            var keyB = TokenStore.BuildStorageKey(agentUrl, tenant: "tenant-b");

            await store.SaveTokenAsync(keyA, new TokenResult
            {
                AccessToken = "tenant-a-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
            });

            await store.SaveTokenAsync(keyB, new TokenResult
            {
                AccessToken = "tenant-b-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
            });

            // Load and verify each tenant's token independently
            var loadedA = await store.LoadTokenAsync(keyA);
            var loadedB = await store.LoadTokenAsync(keyB);

            Assert.NotNull(loadedA);
            Assert.NotNull(loadedB);
            Assert.Equal("tenant-a-token", loadedA.AccessToken);
            Assert.Equal("tenant-b-token", loadedB.AccessToken);
            Assert.NotEqual(loadedA.AccessToken, loadedB.AccessToken);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
