using A2AAsk.Catalog;

namespace A2AAsk.Tests;

public class CatalogRegistryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _registryPath;
    private readonly CatalogRegistry _registry;

    public CatalogRegistryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"a2a-ask-catalog-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _registryPath = Path.Combine(_tempDir, "catalog-aliases.json");
        _registry = new CatalogRegistry(_registryPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void AddAlias_LoadAliases_RoundTrips()
    {
        _registry.AddAlias("intuit", "https://intuit.com");

        var aliases = _registry.LoadAliases();

        Assert.True(aliases.TryGetValue("intuit", out var url));
        Assert.Equal("https://intuit.com", url);
    }

    [Fact]
    public void RemoveAlias_RemovesAlias()
    {
        _registry.AddAlias("intuit", "https://intuit.com");

        var removed = _registry.RemoveAlias("intuit");
        var aliases = _registry.LoadAliases();

        Assert.True(removed);
        Assert.DoesNotContain("intuit", aliases.Keys);
    }

    [Fact]
    public void TryGetUrl_IsCaseInsensitive_AndPreservesOriginalCasing()
    {
        _registry.AddAlias("Intuit", "https://intuit.com");

        var found = _registry.TryGetUrl("intuit", out var url);
        var aliases = _registry.LoadAliases();

        Assert.True(found);
        Assert.Equal("https://intuit.com", url);
        Assert.Contains("Intuit", aliases.Keys);
    }

    [Theory]
    [InlineData("bad@alias")]
    [InlineData("bad://alias")]
    [InlineData("bad alias")]
    [InlineData(" ")]
    public void AddAlias_InvalidAlias_Throws(string alias)
    {
        Assert.Throws<ArgumentException>(() => _registry.AddAlias(alias, "https://example.com"));
    }

    [Fact]
    public void AddAlias_UsesAtomicReplace()
    {
        _registry.AddAlias("first", "https://first.example.com");
        _registry.AddAlias("second", "https://second.example.com");

        var registryFiles = Directory.GetFiles(_tempDir);
        var json = File.ReadAllText(_registryPath);

        Assert.Contains(_registryPath, registryFiles);
        Assert.DoesNotContain(registryFiles, path => path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("second", json, StringComparison.Ordinal);
    }

    [Fact]
    public void TryGetUrl_ReturnsFalse_WhenAliasMissing()
    {
        var found = _registry.TryGetUrl("missing", out var url);

        Assert.False(found);
        Assert.Equal(string.Empty, url);
    }

    [Fact]
    public void MultipleAliases_AreStoredIndependently()
    {
        _registry.AddAlias("intuit", "https://intuit.com");
        _registry.AddAlias("agentbin", "https://agentbin.example.com");

        var aliases = _registry.LoadAliases();

        Assert.Equal(2, aliases.Count);
        Assert.Equal("https://intuit.com", aliases["intuit"]);
        Assert.Equal("https://agentbin.example.com", aliases["agentbin"]);
    }
}
