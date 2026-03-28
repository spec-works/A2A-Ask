using A2A;
using Xunit;

namespace A2AAsk.IntegrationTests;

/// <summary>
/// Tests discover (agent card fetch) against all test agents.
/// </summary>
[Collection("TestServer")]
public class DiscoverTests
{
    private readonly TestServerFixture _fixture;

    public DiscoverTests(TestServerFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/open")]
    [InlineData("/api-key-header")]
    [InlineData("/api-key-cookie")]
    [InlineData("/bearer")]
    [InlineData("/basic")]
    [InlineData("/oauth2-static")]
    [InlineData("/multi-auth")]
    [InlineData("/tenant")]
    [InlineData("/input-required")]
    [InlineData("/auth-required")]
    public async Task Discover_ReturnsAgentCard(string path)
    {
        var response = await _fixture.Client.GetAsync($"{path}/.well-known/agent-card.json");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"name\"", json);
        Assert.Contains("\"version\"", json);
    }

    [Fact]
    public async Task Discover_ApiKeyHeaderAgent_ShowsSecurityScheme()
    {
        var response = await _fixture.Client.GetAsync("/api-key-header/.well-known/agent-card.json");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("apikey-header", json);
        Assert.Contains("X-Api-Key", json);
        Assert.Contains("header", json);
    }

    [Fact]
    public async Task Discover_ApiKeyCookieAgent_ShowsCookieScheme()
    {
        var response = await _fixture.Client.GetAsync("/api-key-cookie/.well-known/agent-card.json");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("cookie", json);
    }

    [Fact]
    public async Task Discover_BearerAgent_ShowsBearerScheme()
    {
        var response = await _fixture.Client.GetAsync("/bearer/.well-known/agent-card.json");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("bearer", json);
    }

    [Fact]
    public async Task Discover_BasicAgent_ShowsBasicScheme()
    {
        var response = await _fixture.Client.GetAsync("/basic/.well-known/agent-card.json");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("basic", json);
    }

    [Fact]
    public async Task Discover_OAuth2Agent_ShowsOAuth2Scheme()
    {
        var response = await _fixture.Client.GetAsync("/oauth2-static/.well-known/agent-card.json");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("oauth2", json);
        Assert.Contains("tokenUrl", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Discover_MultiAuthAgent_ShowsBothSchemes()
    {
        var response = await _fixture.Client.GetAsync("/multi-auth/.well-known/agent-card.json");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("X-Api-Key", json);
        Assert.Contains("bearer", json);
    }
}
