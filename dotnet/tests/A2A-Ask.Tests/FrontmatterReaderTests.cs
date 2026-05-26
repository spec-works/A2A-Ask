using A2AAsk.Catalog;

namespace A2AAsk.Tests;

public class FrontmatterReaderTests
{
    private const string SampleAgentFile = """
---
name: weather
description: Get weather forecasts
tools: ['shell']
remote-agent:
  catalog: https://catalog.acme.com/.well-known/ai-catalog.json
  entry-id: weather
  card-url: https://weather.acme.com/.well-known/agent-card.json
  card-etag: '"a1b2c3"'
  card-hash: abc123def456
  installed-at: 2026-05-24T13:09:00Z
---

# Weather (A2A bridge)
Generated bridge content.
""";

    [Fact]
    public void TryRead_ValidFrontmatter_ExtractsRemoteAgent()
    {
        var success = FrontmatterReader.TryRead(SampleAgentFile, out var frontmatter);

        Assert.True(success);
        Assert.NotNull(frontmatter);
        Assert.Equal("https://catalog.acme.com/.well-known/ai-catalog.json", frontmatter!.Catalog);
        Assert.Equal("weather", frontmatter.EntryId);
        Assert.Equal("https://weather.acme.com/.well-known/agent-card.json", frontmatter.CardUrl);
    }

    [Fact]
    public void TryRead_NoFrontmatter_ReturnsFalse()
    {
        var success = FrontmatterReader.TryRead("# Weather\n\nNo frontmatter here.", out var frontmatter);

        Assert.False(success);
        Assert.Null(frontmatter);
    }

    [Fact]
    public void TryRead_NoRemoteAgentBlock_ReturnsFalse()
    {
        const string markdown = """
---
name: weather
description: Get weather forecasts
tools: ['shell']
---

# Weather
""";

        var success = FrontmatterReader.TryRead(markdown, out var frontmatter);

        Assert.False(success);
        Assert.Null(frontmatter);
    }

    [Fact]
    public void TryRead_OptionalFieldsMissing_DefaultsToNull()
    {
        const string markdown = """
---
remote-agent:
  catalog: https://catalog.acme.com/.well-known/ai-catalog.json
  entry-id: weather
  card-url: https://weather.acme.com/.well-known/agent-card.json
  installed-at: 2026-05-24T13:09:00Z
---
""";

        var success = FrontmatterReader.TryRead(markdown, out var frontmatter);

        Assert.True(success);
        Assert.NotNull(frontmatter);
        Assert.Null(frontmatter!.CardEtag);
        Assert.Null(frontmatter.CardHash);
    }

    [Fact]
    public void TryRead_AllFieldsPresent_AllExtracted()
    {
        var success = FrontmatterReader.TryRead(SampleAgentFile, out var frontmatter);
 
        Assert.True(success);
        Assert.NotNull(frontmatter);
        Assert.Equal("weather", frontmatter!.Name);
        Assert.Equal("https://catalog.acme.com/.well-known/ai-catalog.json", frontmatter.Catalog);
        Assert.Equal("weather", frontmatter.EntryId);
        Assert.Equal("https://weather.acme.com/.well-known/agent-card.json", frontmatter.CardUrl);
        Assert.Equal("\"a1b2c3\"", frontmatter.CardEtag);
        Assert.Equal("abc123def456", frontmatter.CardHash);
        Assert.Equal("2026-05-24T13:09:00Z", frontmatter.InstalledAt);
        Assert.Equal(0, frontmatter.FrontmatterStartLineIndex);
        Assert.Equal(11, frontmatter.FrontmatterEndLineIndex);
        Assert.Equal(4, frontmatter.RemoteAgentStartLineIndex);
        Assert.Equal(10, frontmatter.RemoteAgentEndLineIndex);
    }

    [Fact]
    public void TryRead_WithCardEtag_ExtractsCardEtagValue()
    {
        var success = FrontmatterReader.TryRead(SampleAgentFile, out var frontmatter);

        Assert.True(success);
        Assert.NotNull(frontmatter);
        Assert.Equal("\"a1b2c3\"", frontmatter!.CardEtag);
    }

    [Fact]
    public void TryRead_WithoutCardEtag_ReturnsNullCardEtag()
    {
        const string markdown = """
---
name: weather
remote-agent:
  catalog: https://catalog.acme.com/.well-known/ai-catalog.json
  entry-id: weather
  card-url: https://weather.acme.com/.well-known/agent-card.json
  card-hash: abc123def456
  installed-at: 2026-05-24T13:09:00Z
---
""";

        var success = FrontmatterReader.TryRead(markdown, out var frontmatter);

        Assert.True(success);
        Assert.NotNull(frontmatter);
        Assert.Null(frontmatter!.CardEtag);
        Assert.Equal("abc123def456", frontmatter.CardHash);
        Assert.Equal("2026-05-24T13:09:00Z", frontmatter.InstalledAt);
    }

    [Fact]
    public void TryRead_ExtraFrontmatterFields_IgnoresExtras()
    {
        const string markdown = """
---
name: weather
description: Get weather forecasts
x-extra-field: ignored
remote-agent:
  catalog: https://catalog.acme.com/.well-known/ai-catalog.json
  entry-id: weather
  card-url: https://weather.acme.com/.well-known/agent-card.json
  installed-at: 2026-05-24T13:09:00Z
another-extra:
  nested: true
---
""";

        var success = FrontmatterReader.TryRead(markdown, out var frontmatter);

        Assert.True(success);
        Assert.NotNull(frontmatter);
        Assert.Equal("weather", frontmatter!.EntryId);
        Assert.Equal("https://weather.acme.com/.well-known/agent-card.json", frontmatter.CardUrl);
    }

    [Fact]
    public void TryRead_EmptyFile_ReturnsFalse()
    {
        var success = FrontmatterReader.TryRead(string.Empty, out var frontmatter);

        Assert.False(success);
        Assert.Null(frontmatter);
    }

    [Fact]
    public void TryRead_FrontmatterOnly_NoBody_Works()
    {
        const string markdown = """
---
remote-agent:
  catalog: https://catalog.acme.com/.well-known/ai-catalog.json
  entry-id: weather
  card-url: https://weather.acme.com/.well-known/agent-card.json
  installed-at: 2026-05-24T13:09:00Z
---
""";

        var success = FrontmatterReader.TryRead(markdown, out var frontmatter);

        Assert.True(success);
        Assert.NotNull(frontmatter);
        Assert.Equal("https://catalog.acme.com/.well-known/ai-catalog.json", frontmatter!.Catalog);
        Assert.Equal("weather", frontmatter.EntryId);
    }
}
