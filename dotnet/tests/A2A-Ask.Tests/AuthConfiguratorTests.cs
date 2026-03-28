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

    [Fact]
    public void CreateHttpClient_WithBasicAuth_SetsBasicHeader()
    {
        var client = AuthConfigurator.CreateHttpClient(authUser: "user", authPassword: "pass");

        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.Equal("Basic", client.DefaultRequestHeaders.Authorization.Scheme);
        var decoded = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(client.DefaultRequestHeaders.Authorization.Parameter!));
        Assert.Equal("user:pass", decoded);
    }

    [Fact]
    public void CreateHttpClient_WithBasicAuth_EmptyPassword()
    {
        var client = AuthConfigurator.CreateHttpClient(authUser: "user");

        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.Equal("Basic", client.DefaultRequestHeaders.Authorization.Scheme);
        var decoded = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(client.DefaultRequestHeaders.Authorization.Parameter!));
        Assert.Equal("user:", decoded);
    }

    [Fact]
    public void CreateHttpClient_ApiKeyAsCookie()
    {
        var client = AuthConfigurator.CreateHttpClient(
            apiKey: "secret-key", apiKeyHeader: "session", apiKeyLocation: "cookie");

        Assert.True(client.DefaultRequestHeaders.Contains("Cookie"));
        Assert.Equal("session=secret-key",
            client.DefaultRequestHeaders.GetValues("Cookie").First());
    }

    [Fact]
    public void CreateHttpClient_ApiKeyAsHeader_Default()
    {
        var client = AuthConfigurator.CreateHttpClient(
            apiKey: "secret-key", apiKeyLocation: "header");

        Assert.True(client.DefaultRequestHeaders.Contains("X-API-Key"));
    }
}
