using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace A2AAsk.IntegrationTests;

/// <summary>
/// Shared fixture that hosts the TestAgentServer in-process via WebApplicationFactory.
/// All integration test classes share this single server instance.
/// </summary>
public class TestServerFixture : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;

    public HttpClient Client { get; private set; } = null!;
    public string BaseAddress => Client.BaseAddress!.ToString().TrimEnd('/');

    public Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        Client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a fresh HttpClient with custom configuration (e.g., default headers).
    /// The client shares the same in-process test server.
    /// </summary>
    public HttpClient CreateClient() => _factory!.CreateClient();

    /// <summary>
    /// Creates an HttpClient that wraps the test server handler with a custom DelegatingHandler.
    /// Useful for adding cookies or custom request transformations.
    /// </summary>
    public HttpClient CreateClientWithHandler(DelegatingHandler outerHandler)
    {
        outerHandler.InnerHandler = _factory!.Server.CreateHandler();
        var client = new HttpClient(outerHandler) { BaseAddress = _factory.Server.BaseAddress };
        return client;
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition("TestServer")]
public class TestServerCollection : ICollectionFixture<TestServerFixture> { }
