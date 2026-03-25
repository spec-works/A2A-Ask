using A2A;
using A2AAsk.Output;

namespace A2AAsk.Tests;

public class ArtifactSaverTests : IDisposable
{
    private readonly string _tempDir;

    public ArtifactSaverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"a2a-ask-artifact-test-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public async Task SaveArtifacts_TextPart_SavesToFile()
    {
        var artifacts = new List<Artifact>
        {
            new()
            {
                ArtifactId = "art-1",
                Name = "report",
                Parts = [new Part { Text = "Hello, World!" }]
            }
        };

        await ArtifactSaver.SaveArtifactsAsync(artifacts, _tempDir);

        var files = Directory.GetFiles(_tempDir);
        Assert.Single(files);
        var content = await File.ReadAllTextAsync(files[0]);
        Assert.Equal("Hello, World!", content);
    }

    [Fact]
    public async Task SaveArtifacts_FilePart_SavesBytes()
    {
        var bytes = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        var artifacts = new List<Artifact>
        {
            new()
            {
                ArtifactId = "art-2",
                Name = "data",
                Parts = [new Part
                {
                    Raw = bytes,
                    MediaType = "application/octet-stream",
                    Filename = "data.bin"
                }]
            }
        };

        await ArtifactSaver.SaveArtifactsAsync(artifacts, _tempDir);

        var files = Directory.GetFiles(_tempDir);
        Assert.Single(files);
        var savedBytes = await File.ReadAllBytesAsync(files[0]);
        Assert.Equal(bytes, savedBytes);
    }

    [Fact]
    public async Task SaveArtifacts_CreatesDirectory()
    {
        var outputDir = Path.Combine(_tempDir, "nested", "output");
        var artifacts = new List<Artifact>
        {
            new()
            {
                ArtifactId = "art-3",
                Name = "test",
                Parts = [new Part { Text = "test content" }]
            }
        };

        await ArtifactSaver.SaveArtifactsAsync(artifacts, outputDir);

        Assert.True(Directory.Exists(outputDir));
        Assert.Single(Directory.GetFiles(outputDir));
    }

    [Fact]
    public async Task SaveArtifacts_MultipleParts_UsesSuffix()
    {
        var artifacts = new List<Artifact>
        {
            new()
            {
                ArtifactId = "art-4",
                Name = "multi",
                Parts =
                [
                    new Part { Text = "part one" },
                    new Part { Text = "part two" }
                ]
            }
        };

        await ArtifactSaver.SaveArtifactsAsync(artifacts, _tempDir);

        var files = Directory.GetFiles(_tempDir);
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public async Task SaveArtifacts_UsesArtifactId_WhenNameIsNull()
    {
        var artifacts = new List<Artifact>
        {
            new()
            {
                ArtifactId = "my-artifact-id",
                Parts = [new Part { Text = "content" }]
            }
        };

        await ArtifactSaver.SaveArtifactsAsync(artifacts, _tempDir);

        var files = Directory.GetFiles(_tempDir);
        Assert.Single(files);
        Assert.Contains("my-artifact-id", Path.GetFileName(files[0]));
    }
}
