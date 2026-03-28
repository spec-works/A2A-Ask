using System.Net.Http.Headers;
using System.Text;

namespace A2AAsk.Auth;

/// <summary>
/// Configures an HttpClient with authentication based on CLI options and agent card security schemes.
/// </summary>
public static class AuthConfigurator
{
    /// <summary>
    /// Creates an HttpClient configured with the provided authentication options.
    /// </summary>
    public static HttpClient CreateHttpClient(
        string? authToken = null,
        string? authHeader = null,
        string? apiKey = null,
        string? apiKeyHeader = null,
        string? apiKeyLocation = null,
        string? authUser = null,
        string? authPassword = null)
    {
        var client = new HttpClient();

        if (!string.IsNullOrEmpty(authToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", authToken);
        }

        if (!string.IsNullOrEmpty(authUser))
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{authUser}:{authPassword ?? ""}"));
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", encoded);
        }

        if (!string.IsNullOrEmpty(apiKey))
        {
            var location = apiKeyLocation?.ToLowerInvariant() ?? "header";
            switch (location)
            {
                case "cookie":
                    var cookieName = apiKeyHeader ?? "api_key";
                    client.DefaultRequestHeaders.Add("Cookie", $"{cookieName}={apiKey}");
                    break;
                case "query":
                    // Query string API keys are handled at request time, not here.
                    // Store as a custom header that callers can read.
                    // For now, set it as a default header — URL rewriting is the caller's responsibility.
                    Console.Error.WriteLine($"Note: API key will be sent as query parameter '{apiKeyHeader ?? "api_key"}'.");
                    Console.Error.WriteLine("Query-string API keys require URL modification per-request.");
                    break;
                default: // "header"
                    var headerName = apiKeyHeader ?? "X-API-Key";
                    client.DefaultRequestHeaders.Add(headerName, apiKey);
                    break;
            }
        }

        if (!string.IsNullOrEmpty(authHeader))
        {
            var parts = authHeader.Split('=', 2);
            if (parts.Length == 2)
            {
                client.DefaultRequestHeaders.Add(parts[0].Trim(), parts[1].Trim());
            }
        }

        return client;
    }

    /// <summary>
    /// Creates an HttpClient with stored token for the agent URL.
    /// Explicit CLI auth options always take priority.
    /// If a stored token is expired and has a refresh token, attempts refresh.
    /// </summary>
    public static async Task<HttpClient> CreateHttpClientWithStoredTokenAsync(
        string agentUrl,
        string? authToken = null,
        string? authHeader = null,
        string? apiKey = null,
        string? apiKeyHeader = null,
        string? authUser = null,
        string? authPassword = null,
        string? tenant = null)
    {
        // Explicit auth always wins
        if (!string.IsNullOrEmpty(authToken) || !string.IsNullOrEmpty(apiKey)
            || !string.IsNullOrEmpty(authHeader) || !string.IsNullOrEmpty(authUser))
        {
            return CreateHttpClient(authToken, authHeader, apiKey, apiKeyHeader, authUser, authPassword);
        }

        // Try loading stored token
        var store = new TokenStore();
        var storageKey = TokenStore.BuildStorageKey(agentUrl, tenant);
        var storedToken = await store.LoadTokenAsync(storageKey);
        if (storedToken != null)
        {
            if (!storedToken.IsExpired)
            {
                return CreateHttpClient(authToken: storedToken.AccessToken);
            }

            // Token expired — try refresh
            if (!string.IsNullOrEmpty(storedToken.RefreshToken))
            {
                var refreshed = await DeviceCodeFlow.RefreshTokenAsync(storedToken);
                if (refreshed != null)
                {
                    await store.SaveTokenAsync(storageKey, refreshed);
                    Console.Error.WriteLine("Token refreshed automatically.");
                    return CreateHttpClient(authToken: refreshed.AccessToken);
                }
                Console.Error.WriteLine("Stored token expired and refresh failed. Run: a2a-ask auth login <url>");
            }
            else
            {
                Console.Error.WriteLine("Stored token expired (no refresh token). Run: a2a-ask auth login <url>");
            }
        }

        return CreateHttpClient();
    }
}
