using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace A2AAsk.Auth;

/// <summary>
/// Persists and loads authentication tokens scoped by agent URL.
/// On Windows, tokens are encrypted using DPAPI (current user scope).
/// On other platforms, tokens are stored as plaintext JSON.
/// Store path: ~/.a2a-ask/tokens.dat (encrypted) or tokens.json (plaintext).
/// </summary>
public class TokenStore
{
    private readonly string _storePath;
    private readonly bool _useEncryption;

    public TokenStore(string? storePath = null, bool? useEncryption = null)
    {
        var defaultEncrypt = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        _useEncryption = useEncryption ?? defaultEncrypt;
        _storePath = storePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".a2a-ask",
            _useEncryption ? "tokens.dat" : "tokens.json");
    }

    /// <summary>
    /// Builds a storage key from agent URL and optional tenant.
    /// </summary>
    public static string BuildStorageKey(string agentUrl, string? tenant = null)
    {
        var normalized = new Uri(agentUrl.TrimEnd('/')).ToString().TrimEnd('/').ToLowerInvariant();
        return string.IsNullOrEmpty(tenant) ? normalized : $"{normalized}|tenant={tenant}";
    }

    public async Task SaveTokenAsync(string agentUrl, TokenResult token)
    {
        var tokens = await LoadAllTokensAsync();
        var key = NormalizeUrl(agentUrl);
        tokens[key] = token;
        await SaveAllTokensAsync(tokens);
    }

    public async Task<TokenResult?> LoadTokenAsync(string agentUrl)
    {
        var tokens = await LoadAllTokensAsync();
        var key = NormalizeUrl(agentUrl);
        return tokens.TryGetValue(key, out var token) ? token : null;
    }

    public async Task RemoveTokenAsync(string agentUrl)
    {
        var tokens = await LoadAllTokensAsync();
        var key = NormalizeUrl(agentUrl);
        if (tokens.Remove(key))
            await SaveAllTokensAsync(tokens);
    }

    private async Task SaveAllTokensAsync(Dictionary<string, TokenResult> tokens)
    {
        var dir = Path.GetDirectoryName(_storePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(tokens, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (_useEncryption)
        {
#pragma warning disable CA1416 // Platform compatibility - guarded by _useEncryption check
            var plainBytes = System.Text.Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(_storePath, encrypted);
#pragma warning restore CA1416
        }
        else
        {
            await File.WriteAllTextAsync(_storePath, json);
        }

        // Restrict file permissions on Unix
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            File.SetUnixFileMode(_storePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private async Task<Dictionary<string, TokenResult>> LoadAllTokensAsync()
    {
        if (!File.Exists(_storePath))
        {
            // Migrate from old plaintext store if present
            var legacyPath = Path.Combine(Path.GetDirectoryName(_storePath)!, "tokens.json");
            if (_useEncryption && File.Exists(legacyPath))
                return await MigrateLegacyStoreAsync(legacyPath);
            return new Dictionary<string, TokenResult>();
        }

        try
        {
            string json;
            if (_useEncryption)
            {
#pragma warning disable CA1416 // Platform compatibility - guarded by _useEncryption check
                var encrypted = await File.ReadAllBytesAsync(_storePath);
                var plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                json = System.Text.Encoding.UTF8.GetString(plainBytes);
#pragma warning restore CA1416
            }
            else
            {
                json = await File.ReadAllTextAsync(_storePath);
            }

            return JsonSerializer.Deserialize<Dictionary<string, TokenResult>>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                ?? new Dictionary<string, TokenResult>();
        }
        catch (CryptographicException)
        {
            Console.Error.WriteLine("Warning: Token store is corrupted or inaccessible. Starting fresh.");
            return new Dictionary<string, TokenResult>();
        }
        catch
        {
            return new Dictionary<string, TokenResult>();
        }
    }

    private async Task<Dictionary<string, TokenResult>> MigrateLegacyStoreAsync(string legacyPath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(legacyPath);
            var tokens = JsonSerializer.Deserialize<Dictionary<string, TokenResult>>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                ?? new Dictionary<string, TokenResult>();

            // Save in encrypted format
            await SaveAllTokensAsync(tokens);
            // Remove plaintext file
            File.Delete(legacyPath);
            Console.Error.WriteLine("Migrated token store to encrypted format.");
            return tokens;
        }
        catch
        {
            return new Dictionary<string, TokenResult>();
        }
    }

    private static string NormalizeUrl(string url)
    {
        // If it already contains a pipe (tenant key), don't re-normalize
        if (url.Contains('|'))
            return url.ToLowerInvariant();
        return new Uri(url.TrimEnd('/')).ToString().TrimEnd('/').ToLowerInvariant();
    }
}

/// <summary>
/// Represents stored authentication token data.
/// </summary>
public class TokenResult
{
    public string AccessToken { get; set; } = "";
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? TokenType { get; set; }
    public string? TokenUrl { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;
}
