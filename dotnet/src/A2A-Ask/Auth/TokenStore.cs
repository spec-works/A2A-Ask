using System.Text.Json;

namespace A2AAsk.Auth;

/// <summary>
/// Persists and loads authentication tokens scoped by agent URL.
/// Tokens are stored in ~/.a2a-ask/tokens.json.
/// </summary>
public class TokenStore
{
    private readonly string _storePath;

    public TokenStore(string? storePath = null)
    {
        _storePath = storePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".a2a-ask",
            "tokens.json");
    }

    public async Task SaveTokenAsync(string agentUrl, TokenResult token)
    {
        var tokens = await LoadAllTokensAsync();
        var key = NormalizeUrl(agentUrl);
        tokens[key] = token;

        var dir = Path.GetDirectoryName(_storePath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(tokens, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await File.WriteAllTextAsync(_storePath, json);
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
        {
            var json = JsonSerializer.Serialize(tokens, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            await File.WriteAllTextAsync(_storePath, json);
        }
    }

    private async Task<Dictionary<string, TokenResult>> LoadAllTokensAsync()
    {
        if (!File.Exists(_storePath))
            return new Dictionary<string, TokenResult>();

        try
        {
            var json = await File.ReadAllTextAsync(_storePath);
            return JsonSerializer.Deserialize<Dictionary<string, TokenResult>>(json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                ?? new Dictionary<string, TokenResult>();
        }
        catch
        {
            return new Dictionary<string, TokenResult>();
        }
    }

    private static string NormalizeUrl(string url) =>
        new Uri(url.TrimEnd('/')).ToString().TrimEnd('/').ToLowerInvariant();
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
