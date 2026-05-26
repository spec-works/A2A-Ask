using System.Text.Json;
using A2AAsk.Catalog;

namespace A2AAsk.Tests;

public class BridgeGeneratorTests
{
    private const string Name = "weather-agent";
    private const string DisplayName = "Weather Agent";
    private const string Description = "Get weather forecasts";
    private const string CatalogTarget = "weather@catalog";
    private const string CatalogUrl = "https://catalog.acme.com/.well-known/ai-catalog.json";
    private const string EntryId = "weather";
    private const string AgentCardUrl = "https://weather.acme.com/.well-known/agent-card.json";
    private const string CardEtag = "\"a1b2c3\"";
    private const string CardHash = "abc123def456";
    private const string InstalledAt = "2026-05-24T13:09:00Z";

    [Fact]
    public void GenerateKebabCaseName_SimpleWords_ReturnsKebabCase()
    {
        var result = BridgeGenerator.GenerateKebabCaseName("Weather Agent");

        Assert.Equal("weather-agent", result);
    }

    [Fact]
    public void GenerateKebabCaseName_AlreadyKebab_ReturnsSame()
    {
        var result = BridgeGenerator.GenerateKebabCaseName("weather-agent");

        Assert.Equal("weather-agent", result);
    }

    [Fact]
    public void GenerateKebabCaseName_MixedCaseWithoutSeparators_ReturnsLowercaseLetters()
    {
        var result = BridgeGenerator.GenerateKebabCaseName("MyWeatherBot");

        Assert.Equal("myweatherbot", result);
    }

    [Fact]
    public void GenerateKebabCaseName_SpecialChars_StripsAndKebabs()
    {
        var result = BridgeGenerator.GenerateKebabCaseName("Weather Agent (v2)");

        Assert.Equal("weather-agent-v2", result);
    }

    [Fact]
    public void GenerateKebabCaseName_MultipleSpaces_SingleDash()
    {
        var result = BridgeGenerator.GenerateKebabCaseName("Weather   Agent");

        Assert.Equal("weather-agent", result);
    }

    [Fact]
    public void GenerateKebabCaseName_LeadingTrailingSpaces_Trimmed()
    {
        var result = BridgeGenerator.GenerateKebabCaseName("  Weather Agent  ");

        Assert.Equal("weather-agent", result);
    }

    [Theory]
    [InlineData("explore")]
    [InlineData("task")]
    [InlineData("code-review")]
    [InlineData("general-purpose")]
    [InlineData("research")]
    [InlineData("rubber-duck")]
    public void IsReservedName_ReservedNames_ReturnsTrue(string name)
    {
        var result = BridgeGenerator.IsReservedName(name);

        Assert.True(result);
    }

    [Fact]
    public void IsReservedName_NormalName_ReturnsFalse()
    {
        var result = BridgeGenerator.IsReservedName("weather");

        Assert.False(result);
    }

    [Fact]
    public void IsReservedName_CaseSensitive_ReturnsFalse()
    {
        var result = BridgeGenerator.IsReservedName("Explore");

        Assert.False(result);
    }

    [Fact]
    public void GenerateMarkdown_BasicAgent_ContainsFrontmatter()
    {
        var markdown = new BridgeGenerator().GenerateMarkdown(CreateModel(CatalogTarget, CardEtag, CardHash, []));

        Assert.StartsWith("---", markdown, StringComparison.Ordinal);
        Assert.Contains($"name: {Name}", markdown, StringComparison.Ordinal);
        Assert.Contains("description: 'Get weather forecasts'", markdown, StringComparison.Ordinal);
        Assert.Contains("tools: ['shell']", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMarkdown_BasicAgent_ContainsRemoteAgentBlock()
    {
        var markdown = new BridgeGenerator().GenerateMarkdown(CreateModel(CatalogTarget, CardEtag, CardHash, []));

        Assert.Contains("remote-agent:", markdown, StringComparison.Ordinal);
        Assert.Contains($"  catalog: '{CatalogUrl}'", markdown, StringComparison.Ordinal);
        Assert.Contains($"  entry-id: '{EntryId}'", markdown, StringComparison.Ordinal);
        Assert.Contains($"  card-url: '{AgentCardUrl}'", markdown, StringComparison.Ordinal);
        Assert.Contains($"  installed-at: '{InstalledAt}'", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMarkdown_WithEtag_IncludesQuotedEtag()
    {
        var markdown = new BridgeGenerator().GenerateMarkdown(CreateModel(CatalogTarget, CardEtag, CardHash, []));

        Assert.Contains($"  card-etag: '{CardEtag}'", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMarkdown_WithoutEtag_OmitsEtag()
    {
        var markdown = new BridgeGenerator().GenerateMarkdown(CreateModel(CatalogTarget, null, CardHash, []));

        Assert.DoesNotContain("card-etag:", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMarkdown_WithHash_IncludesQuotedHash()
    {
        var markdown = new BridgeGenerator().GenerateMarkdown(CreateModel(CatalogTarget, CardEtag, CardHash, []));

        Assert.Contains($"  card-hash: '{CardHash}'", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMarkdown_WithSkills_ListsSkillsAndUpdatesDescription()
    {
        var markdown = new BridgeGenerator().GenerateMarkdown(CreateModel(
            CatalogTarget,
            CardEtag,
            CardHash,
            [
                new BridgeAgentSkill("forecast", "Forecast", "Get forecasts."),
                new BridgeAgentSkill("alerts", "Alerts", null)
            ]));

        Assert.Contains("description: 'Get weather forecasts. Skills: Forecast, Alerts.'", markdown, StringComparison.Ordinal);
        Assert.Contains("## Skills declared by the agent card", markdown, StringComparison.Ordinal);
        Assert.Contains("- `forecast` — Get forecasts.", markdown, StringComparison.Ordinal);
        Assert.Contains("- `alerts` — No description provided.", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMarkdown_NoSkills_ListsNoneDeclared()
    {
        var markdown = new BridgeGenerator().GenerateMarkdown(CreateModel(CatalogTarget, CardEtag, CardHash, []));

        Assert.Contains("## Skills declared by the agent card", markdown, StringComparison.Ordinal);
        Assert.Contains("- None declared.", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMarkdown_ContainsBeginEndMarkers()
    {
        var markdown = new BridgeGenerator().GenerateMarkdown(CreateModel(CatalogTarget, CardEtag, CardHash, []));

        Assert.Contains(BridgeGenerator.BeginGeneratedMarker, markdown, StringComparison.Ordinal);
        Assert.Contains(BridgeGenerator.EndGeneratedMarker, markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateGeneratedSection_ContainsBridgeInstructions()
    {
        var generatedSection = new BridgeGenerator().GenerateGeneratedSection(CreateModel(CatalogTarget, CardEtag, CardHash, []));

        Assert.StartsWith(BridgeGenerator.BeginGeneratedMarker, generatedSection, StringComparison.Ordinal);
        Assert.Contains("## Your job", generatedSection, StringComparison.Ordinal);
        Assert.Contains("## Rules", generatedSection, StringComparison.Ordinal);
        Assert.EndsWith(BridgeGenerator.EndGeneratedMarker, generatedSection, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMarkdown_CatalogTarget_UsesCatalogTargetInCommands()
    {
        var markdown = new BridgeGenerator().GenerateMarkdown(CreateModel(CatalogTarget, CardEtag, CardHash, []));

        Assert.Contains($"a2a-ask send \"{CatalogTarget}\"", markdown, StringComparison.Ordinal);
        Assert.Contains($"a2a-ask auth login \"{CatalogTarget}\"", markdown, StringComparison.Ordinal);
        Assert.Contains($"a2a-ask stream \"{CatalogTarget}\"", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateMarkdown_DirectUrl_UsesUrlInCommands()
    {
        var markdown = new BridgeGenerator().GenerateMarkdown(CreateModel(AgentCardUrl, CardEtag, CardHash, []));
 
        Assert.Contains($"a2a-ask send \"{AgentCardUrl}\"", markdown, StringComparison.Ordinal);
        Assert.Contains($"a2a-ask auth login \"{AgentCardUrl}\"", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain($"a2a-ask send \"{CatalogTarget}\"", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceGeneratedSection_ReplacesOnlyGeneratedBlock_PreservesCustomContent()
    {
        var generator = new BridgeGenerator();
        var original = generator.GenerateMarkdown(CreateModel(CatalogTarget, CardEtag, CardHash, []))
            + Environment.NewLine
            + "## Custom Notes"
            + Environment.NewLine
            + "Keep this section."
            + Environment.NewLine;
        var updatedSection = generator.GenerateGeneratedSection(CreateModel(
            CatalogTarget,
            CardEtag,
            CardHash,
            [new BridgeAgentSkill("forecast", "Forecast", "Get forecasts.")]));

        var replaced = BridgeGenerator.ReplaceGeneratedSection(original, updatedSection);

        Assert.Contains("- `forecast` — Get forecasts.", replaced, StringComparison.Ordinal);
        Assert.DoesNotContain("- None declared.", replaced, StringComparison.Ordinal);
        Assert.Contains("## Custom Notes", replaced, StringComparison.Ordinal);
        Assert.Contains("Keep this section.", replaced, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseAgentCard_MissingSkillId_GeneratesKebabCaseIdAndFlagsCapabilities()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "name": "Weather Agent",
              "description": "Provides forecasts",
              "skills": [
                {
                  "name": "Severe Weather Alerts",
                  "description": " Sends alerts. "
                }
              ],
              "securitySchemes": {
                "apiKey": {
                  "type": "apiKey"
                }
              },
              "capabilities": {
                "streaming": true
              }
            }
            """);

        var card = BridgeGenerator.ParseAgentCard(document.RootElement);

        Assert.Equal("Weather Agent", card.Name);
        Assert.Equal("Provides forecasts", card.Description);
        var skill = Assert.Single(card.Skills);
        Assert.Equal("severe-weather-alerts", skill.Id);
        Assert.Equal("Severe Weather Alerts", skill.Name);
        Assert.Equal(" Sends alerts. ", skill.Description);
        Assert.True(card.RequiresAuthentication);
        Assert.True(card.SupportsStreaming);
    }
 
    private static BridgeTemplateModel CreateModel(string catalogTarget, string? etag, string? hash, IReadOnlyList<BridgeAgentSkill> skills) =>
        new(
            Name,
            catalogTarget,
            DisplayName,
            new BridgeAgentCard(DisplayName, Description, skills, RequiresAuthentication: false, SupportsStreaming: true),
            new BridgeRemoteAgentMetadata(CatalogUrl, EntryId, AgentCardUrl, etag, hash, InstalledAt));
}
