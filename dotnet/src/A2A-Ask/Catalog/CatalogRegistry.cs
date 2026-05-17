using System.Runtime.InteropServices;
using System.Text.Json;

namespace A2AAsk.Catalog;

/// <summary>
/// Persists user-defined catalog aliases.
/// </summary>
public class CatalogRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogRegistry"/> class.
    /// </summary>
    /// <param name="filePath">The optional registry file path.</param>
    public CatalogRegistry(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".a2a-ask",
            "catalog-aliases.json");
    }

    /// <summary>
    /// Loads all registered catalog aliases.
    /// </summary>
    /// <returns>The registered aliases keyed by alias name.</returns>
    public Dictionary<string, string> LoadAliases()
    {
        if (!File.Exists(_filePath))
        {
            return CreateAliasDictionary();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var aliases = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            return new Dictionary<string, string>(aliases, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return CreateAliasDictionary();
        }
        catch
        {
            return CreateAliasDictionary();
        }
    }

    /// <summary>
    /// Adds or updates a catalog alias.
    /// </summary>
    /// <param name="alias">The catalog alias.</param>
    /// <param name="url">The catalog URL.</param>
    public void AddAlias(string alias, string url)
    {
        var validatedAlias = ValidateAlias(alias);
        var validatedUrl = string.IsNullOrWhiteSpace(url)
            ? throw new ArgumentException("Catalog URL must not be empty.", nameof(url))
            : url.Trim();

        var aliases = LoadAliases();
        var existingAlias = aliases.Keys.FirstOrDefault(key => string.Equals(key, validatedAlias, StringComparison.OrdinalIgnoreCase));
        if (existingAlias != null)
        {
            aliases.Remove(existingAlias);
        }

        aliases[validatedAlias] = validatedUrl;
        SaveAliases(aliases);
    }

    /// <summary>
    /// Removes a registered catalog alias.
    /// </summary>
    /// <param name="alias">The alias to remove.</param>
    /// <returns><see langword="true"/> when the alias existed; otherwise <see langword="false"/>.</returns>
    public bool RemoveAlias(string alias)
    {
        var validatedAlias = ValidateAlias(alias);
        var aliases = LoadAliases();
        var existingAlias = aliases.Keys.FirstOrDefault(key => string.Equals(key, validatedAlias, StringComparison.OrdinalIgnoreCase));
        if (existingAlias == null)
        {
            return false;
        }

        aliases.Remove(existingAlias);
        SaveAliases(aliases);
        return true;
    }

    /// <summary>
    /// Attempts to resolve a catalog alias to its registered URL.
    /// </summary>
    /// <param name="alias">The alias to resolve.</param>
    /// <param name="url">The resolved URL when found.</param>
    /// <returns><see langword="true"/> when the alias exists; otherwise <see langword="false"/>.</returns>
    public bool TryGetUrl(string alias, out string url)
    {
        var normalizedAlias = NormalizeAlias(alias);
        if (string.IsNullOrEmpty(normalizedAlias))
        {
            url = string.Empty;
            return false;
        }

        var aliases = LoadAliases();
        if (aliases.TryGetValue(normalizedAlias, out var resolvedUrl))
        {
            url = resolvedUrl;
            return true;
        }

        url = string.Empty;
        return false;
    }

    internal static string ValidateAlias(string alias)
    {
        var normalizedAlias = NormalizeAlias(alias);
        if (string.IsNullOrEmpty(normalizedAlias))
        {
            throw new ArgumentException("Catalog alias must not be empty.", nameof(alias));
        }

        if (normalizedAlias.Contains('@'))
        {
            throw new ArgumentException("Catalog alias must not contain '@'.", nameof(alias));
        }

        if (normalizedAlias.Contains("://", StringComparison.Ordinal))
        {
            throw new ArgumentException("Catalog alias must not contain '://'.", nameof(alias));
        }

        if (normalizedAlias.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException("Catalog alias must not contain whitespace.", nameof(alias));
        }

        return normalizedAlias;
    }

    private void SaveAliases(Dictionary<string, string> aliases)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Catalog registry path is invalid.");
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(aliases, JsonOptions);
        var tempFilePath = Path.Combine(directory, $"{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, _filePath, true);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(_filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    private static Dictionary<string, string> CreateAliasDictionary() =>
        new(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeAlias(string alias) => alias?.Trim() ?? string.Empty;
}
