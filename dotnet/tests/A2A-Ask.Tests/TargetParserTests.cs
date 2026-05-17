using A2AAsk.Catalog;

namespace A2AAsk.Tests;

public class TargetParserTests
{
    [Fact]
    public void Parse_DirectUrl_ReturnsDirectUrl()
    {
        var result = TargetParser.Parse("https://example.com");

        var directUrl = Assert.IsType<DirectUrl>(result);
        Assert.Equal("https://example.com", directUrl.Url);
    }

    [Fact]
    public void Parse_AgentOnly_ReturnsCatalogTargetWithoutAlias()
    {
        var result = TargetParser.Parse("@tax");

        var catalogTarget = Assert.IsType<CatalogTarget>(result);
        Assert.Equal("tax", catalogTarget.AgentName);
        Assert.Null(catalogTarget.CatalogAlias);
    }

    [Fact]
    public void Parse_AgentAndCatalog_ReturnsCatalogTarget()
    {
        var result = TargetParser.Parse("@tax@intuit");

        var catalogTarget = Assert.IsType<CatalogTarget>(result);
        Assert.Equal("tax", catalogTarget.AgentName);
        Assert.Equal("intuit", catalogTarget.CatalogAlias);
    }

    [Fact]
    public void Parse_BrowseTarget_ReturnsCatalogBrowse()
    {
        var result = TargetParser.Parse("@@intuit");

        var browse = Assert.IsType<CatalogBrowse>(result);
        Assert.Equal("intuit", browse.CatalogAlias);
    }

    [Fact]
    public void IsOriginOnlyUrl_RootUrl_ReturnsTrue()
    {
        var result = TargetParser.IsOriginOnlyUrl("https://example.com", out var uri);

        Assert.True(result);
        Assert.NotNull(uri);
    }
}
