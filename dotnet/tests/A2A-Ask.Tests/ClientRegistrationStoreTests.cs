using A2AAsk.Auth;

namespace A2AAsk.Tests;

public class ClientRegistrationStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storePath;
    private readonly ClientRegistrationStore _store;

    public ClientRegistrationStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"a2a-ask-client-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _storePath = Path.Combine(_tempDir, "clients.json");
        _store = new ClientRegistrationStore(_storePath, useEncryption: false);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task RegisterAndFind_RoundTrips()
    {
        await _store.RegisterClientAsync(new ClientRegistration
        {
            ClientId = "cli-app",
            Issuer = "https://login.example.com/common/v2.0/",
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });

        var registration = await _store.FindClientAsync("https://LOGIN.EXAMPLE.COM/common/v2.0");

        Assert.NotNull(registration);
        Assert.Equal("cli-app", registration.ClientId);
        Assert.Equal("https://login.example.com/common/v2.0", registration.Issuer);
    }

    [Fact]
    public async Task FindClient_PrefersExactResourceMatch()
    {
        await _store.RegisterClientAsync(new ClientRegistration
        {
            ClientId = "default-client",
            Issuer = "https://login.example.com/common/v2.0"
        });
        await _store.RegisterClientAsync(new ClientRegistration
        {
            ClientId = "resource-client",
            Issuer = "https://login.example.com/common/v2.0",
            Resource = "https://graph.microsoft.com/"
        });

        var registration = await _store.FindClientAsync(
            "https://login.example.com/common/v2.0",
            "https://graph.microsoft.com");

        Assert.NotNull(registration);
        Assert.Equal("resource-client", registration.ClientId);
        Assert.Equal("https://graph.microsoft.com", registration.Resource);
    }

    [Fact]
    public async Task FindClient_FallsBackToIssuerWideRegistration()
    {
        await _store.RegisterClientAsync(new ClientRegistration
        {
            ClientId = "default-client",
            Issuer = "https://login.example.com/common/v2.0"
        });
        await _store.RegisterClientAsync(new ClientRegistration
        {
            ClientId = "resource-client",
            Issuer = "https://login.example.com/common/v2.0",
            Resource = "https://graph.microsoft.com"
        });

        var registration = await _store.FindClientAsync(
            "https://login.example.com/common/v2.0",
            "https://management.azure.com");

        Assert.NotNull(registration);
        Assert.Equal("default-client", registration.ClientId);
    }

    [Fact]
    public async Task FindClient_DoesNotMatchResourceScopedRegistration_WhenNoResourceRequested()
    {
        await _store.RegisterClientAsync(new ClientRegistration
        {
            ClientId = "resource-client",
            Issuer = "https://login.example.com/common/v2.0",
            Resource = "https://graph.microsoft.com"
        });

        var registration = await _store.FindClientAsync("https://login.example.com/common/v2.0");

        Assert.Null(registration);
    }

    [Fact]
    public async Task RemoveClient_RemovesMatchingRegistration()
    {
        await _store.RegisterClientAsync(new ClientRegistration
        {
            ClientId = "resource-client",
            Issuer = "https://login.example.com/common/v2.0",
            Resource = "https://graph.microsoft.com"
        });

        var removed = await _store.RemoveClientAsync(
            "https://login.example.com/common/v2.0",
            "https://graph.microsoft.com/");
        var registration = await _store.FindClientAsync(
            "https://login.example.com/common/v2.0",
            "https://graph.microsoft.com");

        Assert.True(removed);
        Assert.Null(registration);
    }
}
