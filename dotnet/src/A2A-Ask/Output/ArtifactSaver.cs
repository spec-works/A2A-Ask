using A2A;

namespace A2AAsk.Output;

/// <summary>
/// Saves artifact content to disk.
/// </summary>
public static class ArtifactSaver
{
    public static async Task SaveArtifactsAsync(IList<Artifact> artifacts, string outputDir)
    {
        Directory.CreateDirectory(outputDir);

        foreach (var artifact in artifacts)
        {
            var baseName = artifact.Name
                ?? artifact.ArtifactId
                ?? Guid.NewGuid().ToString();

            for (int i = 0; i < artifact.Parts.Count; i++)
            {
                var part = artifact.Parts[i];
                var suffix = artifact.Parts.Count > 1 ? $"_{i}" : "";
                string filePath;

                if (part.ContentCase == PartContentCase.Raw && part.Raw != null)
                {
                    var ext = GetExtension(part.MediaType) ?? ".bin";
                    var fileName = part.Filename ?? $"{baseName}{suffix}{ext}";
                    filePath = Path.Combine(outputDir, SanitizeFileName(fileName));
                    await File.WriteAllBytesAsync(filePath, part.Raw);
                    Console.WriteLine($"  Saved: {filePath} ({part.Raw.Length} bytes)");
                }
                else if (part.ContentCase == PartContentCase.Url && part.Url != null)
                {
                    Console.WriteLine($"  Artifact URL: {part.Url} (download manually)");
                }
                else if (part.ContentCase == PartContentCase.Text && part.Text != null)
                {
                    var ext = ".txt";
                    var fileName = $"{baseName}{suffix}{ext}";
                    filePath = Path.Combine(outputDir, SanitizeFileName(fileName));
                    await File.WriteAllTextAsync(filePath, part.Text);
                    Console.WriteLine($"  Saved: {filePath}");
                }
                else if (part.ContentCase == PartContentCase.Data)
                {
                    var fileName = $"{baseName}{suffix}.json";
                    filePath = Path.Combine(outputDir, SanitizeFileName(fileName));
                    var json = System.Text.Json.JsonSerializer.Serialize(part.Data);
                    await File.WriteAllTextAsync(filePath, json);
                    Console.WriteLine($"  Saved: {filePath}");
                }
            }
        }
    }

    private static string? GetExtension(string? mediaType) => mediaType switch
    {
        "text/plain" => ".txt",
        "text/markdown" => ".md",
        "text/html" => ".html",
        "text/csv" => ".csv",
        "application/json" => ".json",
        "application/xml" => ".xml",
        "application/pdf" => ".pdf",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/svg+xml" => ".svg",
        _ => null
    };

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
}
