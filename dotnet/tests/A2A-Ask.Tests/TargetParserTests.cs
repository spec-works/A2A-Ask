using A2AAsk.Catalog;

namespace A2AAsk.Tests;

public class TargetParserTests
{
    [Fact]
    public void Parse_QualifiedReference_ReturnsCatalogTarget()
    {
        var result = TargetParser.Parse("agent@catalog");

        var catalogTarget = Assert.IsType<CatalogTarget>(result);
        Assert.Equal("agent", catalogTarget.AgentName);
        Assert.Equal("catalog", catalogTarget.CatalogAlias);
    }

    [Fact]
    public void Parse_QualifiedReference_DecodesSpaces()
    {
        var result = TargetParser.Parse("my+cool+agent@my+catalog");

        var catalogTarget = Assert.IsType<CatalogTarget>(result);
        Assert.Equal("my cool agent", catalogTarget.AgentName);
        Assert.Equal("my catalog", catalogTarget.CatalogAlias);
    }

    [Fact]
    public void Parse_BareName_ReturnsUnqualifiedName()
    {
        var result = TargetParser.Parse("mycatalog");

        var unqualified = Assert.IsType<UnqualifiedName>(result);
        Assert.Equal("mycatalog", unqualified.Name);
    }

    [Fact]
    public void Parse_BareName_DecodesSpaces()
    {
        var result = TargetParser.Parse("my+catalog");

        var unqualified = Assert.IsType<UnqualifiedName>(result);
        Assert.Equal("my catalog", unqualified.Name);
    }

    [Fact]
    public void Parse_LocalhostWithPort_ReturnsDirectUrl()
    {
        var result = TargetParser.Parse("localhost:5000");

        var directUrl = Assert.IsType<DirectUrl>(result);
        Assert.Equal("localhost:5000", directUrl.Url);
    }

    [Fact]
    public void Parse_DomainName_ReturnsDirectUrl()
    {
        var result = TargetParser.Parse("example.com");

        var directUrl = Assert.IsType<DirectUrl>(result);
        Assert.Equal("example.com", directUrl.Url);
    }

    [Fact]
    public void Parse_DomainPath_ReturnsDirectUrl()
    {
        var result = TargetParser.Parse("example.com/path");

        var directUrl = Assert.IsType<DirectUrl>(result);
        Assert.Equal("example.com/path", directUrl.Url);
    }

    [Fact]
    public void Parse_HttpsUrl_ReturnsDirectUrl()
    {
        var result = TargetParser.Parse("https://foo.com");

        var directUrl = Assert.IsType<DirectUrl>(result);
        Assert.Equal("https://foo.com", directUrl.Url);
    }

    [Fact]
    public void Parse_AgentWithEmptyCatalog_Throws()
    {
        Assert.Throws<ArgumentException>(() => TargetParser.Parse("agent@"));
    }

    [Fact]
    public void Parse_EmptyAgentWithCatalog_Throws()
    {
        Assert.Throws<ArgumentException>(() => TargetParser.Parse("@catalog"));
    }

    [Fact]
    public void Parse_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => TargetParser.Parse(string.Empty));
    }

    [Fact]
    public void IsOriginOnlyUrl_RootUrl_ReturnsTrue()
    {
        var result = TargetParser.IsOriginOnlyUrl("https://example.com", out var uri);

        Assert.True(result);
        Assert.NotNull(uri);
    }
}
