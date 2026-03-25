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

        // Bearer token takes priority
        if (!string.IsNullOrEmpty(authToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", authToken);
        }

        // API key
        if (!string.IsNullOrEmpty(apiKey))
        {
            var headerName = apiKeyHeader ?? "X-API-Key";
            client.DefaultRequestHeaders.Add(headerName, apiKey);
        }

        // Custom auth header (key=value format)
        if (!string.IsNullOrEmpty(authHeader))
        {
            var parts = authHeader.Split('=', 2);
            if (parts.Length == 2)
            {
                client.DefaultRequestHeaders.Add(parts[0].Trim(), parts[1].Trim());
            }
        }

        // Try loading stored token if no explicit auth was provided
        if (string.IsNullOrEmpty(authToken) && string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(authHeader))
        {
            // Token store integration is handled at the command level
            // where we have the agent URL context
        }

        return client;
    }

    /// <summary>
    /// Creates an HttpClient configured with a stored token for the given agent URL.
    /// Falls back to the provided CLI options.
    /// </summary>
    public static async Task<HttpClient> CreateHttpClientWithStoredTokenAsync(
        string agentUrl,
        string? authToken = null,
        string? authHeader = null,
        string? apiKey = null,
        string? apiKeyHeader = null)
    {
        // If explicit auth is provided, use it
        if (!string.IsNullOrEmpty(authToken) || !string.IsNullOrEmpty(apiKey) || !string.IsNullOrEmpty(authHeader))
        {
            return CreateHttpClient(authToken, authHeader, apiKey, apiKeyHeader);
        }

        // Try loading stored token
        var store = new TokenStore();
        var storedToken = await store.LoadTokenAsync(agentUrl);
        if (storedToken != null && !storedToken.IsExpired)
        {
            return CreateHttpClient(authToken: storedToken.AccessToken);
        }

        return CreateHttpClient();
    }
}
