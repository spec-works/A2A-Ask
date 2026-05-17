using A2AAsk.Catalog;
using A2AAsk.Commands;
using Xunit;

namespace A2AAsk.IntegrationTests;

[Collection("TestServer")]
public class CatalogInputResolverTests
{
    private readonly TestServerFixture _fixture;

    public CatalogInputResolverTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ResolveAgentsAsync_RootCatalog_ReturnsSingleA2AAgent()
    {
        var resolver = new CatalogInputResolver(_fixture.Client);

        var agents = await resolver.ResolveAgentsAsync(_fixture.BaseAddress);

        var agent = Assert.Single(agents);
        Assert.Equal("open", agent.EntryId);
        Assert.Equal($"{_fixture.BaseAddress}/open/.well-known/agent-card.json", agent.AgentCardUrl);
    }

    [Fact]
    public async Task ResolveAgentsAsync_RelativeCatalog_ResolvesRelativeEntryUrlAgainstCatalogDocument()
    {
        var resolver = new CatalogInputResolver(_fixture.Client);

        var agents = await resolver.ResolveAgentsAsync($"{_fixture.BaseAddress}/catalogs/relative");

        var agent = Assert.Single(agents);
        Assert.Equal($"{_fixture.BaseAddress}/open/.well-known/agent-card.json", agent.AgentCardUrl);
    }

    [Fact]
    public async Task ResolveAgentAsync_MatchesByTagBeforeSubstring()
    {
        var resolver = new CatalogInputResolver(_fixture.Client);

        var agent = await resolver.ResolveAgentAsync($"{_fixture.BaseAddress}/catalogs/multi", "workflow");

        Assert.Equal("input-required", agent.EntryId);
    }

    [Fact]
    public async Task ResolveTargetAsync_OriginUrlAutoSelectsSingleCatalogAgent()
    {
        var resolved = await CommonOptions.ResolveTargetAsync(_fixture.BaseAddress, _fixture.Client);

        Assert.Equal($"{_fixture.BaseAddress}/open", resolved.RequestUrl);
        Assert.Equal($"{_fixture.BaseAddress}/open/.well-known/agent-card.json", resolved.AgentCardUrl);
    }

    [Fact]
    public async Task ResolveTargetAsync_AgentAtCatalogHostAlias_ResolvesAgent()
    {
        var authority = new Uri(_fixture.BaseAddress).Authority;

        var resolved = await CommonOptions.ResolveTargetAsync($"open@{authority}", _fixture.Client);

        Assert.Equal($"{_fixture.BaseAddress}/open", resolved.RequestUrl);
    }
}
