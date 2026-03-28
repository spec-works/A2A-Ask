using A2AAsk.Auth;

namespace A2AAsk.Tests;

public class TokenStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storePath;
    private readonly TokenStore _store;

    public TokenStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"a2a-ask-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _storePath = Path.Combine(_tempDir, "tokens.json");
        _store = new TokenStore(_storePath, useEncryption: false);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var token = new TokenResult
        {
            AccessToken = "test-access-token",
            RefreshToken = "test-refresh-token",
            TokenType = "Bearer",
            ExpiresAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        await _store.SaveTokenAsync("https://agent.example.com", token);
        var loaded = await _store.LoadTokenAsync("https://agent.example.com");

        Assert.NotNull(loaded);
        Assert.Equal("test-access-token", loaded.AccessToken);
        Assert.Equal("test-refresh-token", loaded.RefreshToken);
        Assert.Equal("Bearer", loaded.TokenType);
    }

    [Fact]
    public async Task LoadToken_ReturnsNull_WhenNotFound()
    {
        var loaded = await _store.LoadTokenAsync("https://nonexistent.example.com");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task SaveToken_NormalizesUrl()
    {
        var token = new TokenResult { AccessToken = "token1" };

        await _store.SaveTokenAsync("https://Agent.Example.COM/", token);
        var loaded = await _store.LoadTokenAsync("https://agent.example.com");

        Assert.NotNull(loaded);
        Assert.Equal("token1", loaded.AccessToken);
    }

    [Fact]
    public async Task SaveToken_OverwritesExisting()
    {
        await _store.SaveTokenAsync("https://example.com", new TokenResult { AccessToken = "old" });
        await _store.SaveTokenAsync("https://example.com", new TokenResult { AccessToken = "new" });

        var loaded = await _store.LoadTokenAsync("https://example.com");
        Assert.NotNull(loaded);
        Assert.Equal("new", loaded.AccessToken);
    }

    [Fact]
    public async Task RemoveToken_RemovesExisting()
    {
        await _store.SaveTokenAsync("https://example.com", new TokenResult { AccessToken = "token" });
        await _store.RemoveTokenAsync("https://example.com");

        var loaded = await _store.LoadTokenAsync("https://example.com");
        Assert.Null(loaded);
    }

    [Fact]
    public async Task RemoveToken_NoOp_WhenNotFound()
    {
        // Should not throw
        await _store.RemoveTokenAsync("https://nonexistent.example.com");
    }

    [Fact]
    public async Task MultipleAgents_StoredIndependently()
    {
        await _store.SaveTokenAsync("https://agent1.example.com", new TokenResult { AccessToken = "t1" });
        await _store.SaveTokenAsync("https://agent2.example.com", new TokenResult { AccessToken = "t2" });

        var t1 = await _store.LoadTokenAsync("https://agent1.example.com");
        var t2 = await _store.LoadTokenAsync("https://agent2.example.com");

        Assert.Equal("t1", t1!.AccessToken);
        Assert.Equal("t2", t2!.AccessToken);
    }

    [Fact]
    public void BuildStorageKey_WithoutTenant_ReturnsNormalizedUrl()
    {
        var key = TokenStore.BuildStorageKey("https://Agent.Example.COM/path/");
        Assert.Equal("https://agent.example.com/path", key);
    }

    [Fact]
    public void BuildStorageKey_WithTenant_IncludesTenant()
    {
        var key = TokenStore.BuildStorageKey("https://agent.example.com", "tenant-123");
        Assert.Contains("|tenant=tenant-123", key);
    }

    [Fact]
    public async Task TenantAwareKeys_StoredSeparately()
    {
        var key1 = TokenStore.BuildStorageKey("https://agent.example.com", "tenant-a");
        var key2 = TokenStore.BuildStorageKey("https://agent.example.com", "tenant-b");

        await _store.SaveTokenAsync(key1, new TokenResult { AccessToken = "token-a" });
        await _store.SaveTokenAsync(key2, new TokenResult { AccessToken = "token-b" });

        var ta = await _store.LoadTokenAsync(key1);
        var tb = await _store.LoadTokenAsync(key2);

        Assert.Equal("token-a", ta!.AccessToken);
        Assert.Equal("token-b", tb!.AccessToken);
    }

    [Fact]
    public async Task TokenUrl_IsPersistedAndLoaded()
    {
        var token = new TokenResult
        {
            AccessToken = "at",
            TokenUrl = "https://auth.example.com/token"
        };

        await _store.SaveTokenAsync("https://agent.example.com", token);
        var loaded = await _store.LoadTokenAsync("https://agent.example.com");

        Assert.Equal("https://auth.example.com/token", loaded!.TokenUrl);
    }
}
