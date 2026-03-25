using A2AAsk.Auth;
using System.Net.Http.Headers;

namespace A2AAsk.Tests;

public class AuthConfiguratorTests
{
    [Fact]
    public void CreateHttpClient_WithBearerToken_SetsAuthHeader()
    {
        var client = AuthConfigurator.CreateHttpClient(authToken: "my-token");

        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization.Scheme);
        Assert.Equal("my-token", client.DefaultRequestHeaders.Authorization.Parameter);
    }

    [Fact]
    public void CreateHttpClient_WithApiKey_SetsCustomHeader()
    {
        var client = AuthConfigurator.CreateHttpClient(apiKey: "sk-123", apiKeyHeader: "X-Custom-Key");

        Assert.True(client.DefaultRequestHeaders.Contains("X-Custom-Key"));
        Assert.Equal("sk-123", client.DefaultRequestHeaders.GetValues("X-Custom-Key").First());
    }

    [Fact]
    public void CreateHttpClient_WithApiKey_DefaultsToXApiKey()
    {
        var client = AuthConfigurator.CreateHttpClient(apiKey: "sk-123");

        Assert.True(client.DefaultRequestHeaders.Contains("X-API-Key"));
        Assert.Equal("sk-123", client.DefaultRequestHeaders.GetValues("X-API-Key").First());
    }

    [Fact]
    public void CreateHttpClient_WithAuthHeader_ParsesKeyValue()
    {
        var client = AuthConfigurator.CreateHttpClient(authHeader: "X-Secret=my-secret-value");

        Assert.True(client.DefaultRequestHeaders.Contains("X-Secret"));
        Assert.Equal("my-secret-value", client.DefaultRequestHeaders.GetValues("X-Secret").First());
    }

    [Fact]
    public void CreateHttpClient_NoAuth_ReturnsCleanClient()
    {
        var client = AuthConfigurator.CreateHttpClient();

        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public void CreateHttpClient_BearerTokenTakesPriority()
    {
        var client = AuthConfigurator.CreateHttpClient(
            authToken: "bearer-token",
            apiKey: "api-key");

        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.Equal("bearer-token", client.DefaultRequestHeaders.Authorization.Parameter);
        // API key is also set (both can coexist)
        Assert.True(client.DefaultRequestHeaders.Contains("X-API-Key"));
    }
}
