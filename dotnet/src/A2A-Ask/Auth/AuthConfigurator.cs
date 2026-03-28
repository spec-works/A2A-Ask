using System.Net.Http.Headers;

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
        string? apiKeyHeader = null)
    {
        var client = new HttpClient();

        if (!string.IsNullOrEmpty(authToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", authToken);
        }

        if (!string.IsNullOrEmpty(apiKey))
        {
            var headerName = apiKeyHeader ?? "X-API-Key";
            client.DefaultRequestHeaders.Add(headerName, apiKey);
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
        string? apiKeyHeader = null)
    {
        // Explicit auth always wins
        if (!string.IsNullOrEmpty(authToken) || !string.IsNullOrEmpty(apiKey) || !string.IsNullOrEmpty(authHeader))
        {
            return CreateHttpClient(authToken, authHeader, apiKey, apiKeyHeader);
        }

        // Try loading stored token
        var store = new TokenStore();
        var storedToken = await store.LoadTokenAsync(agentUrl);
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
                    await store.SaveTokenAsync(agentUrl, refreshed);
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
