using System.Net.Http.Headers;
using System.Text;
using A2A;

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
        var location = apiKeyLocation?.ToLowerInvariant() ?? "header";
        var handler = !string.IsNullOrEmpty(apiKey) && string.Equals(location, "query", StringComparison.OrdinalIgnoreCase)
            ? new ApiKeyQueryParameterHandler(apiKeyHeader ?? "api_key", apiKey)
            : null;
        var client = handler == null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: true);

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
            switch (location)
            {
                case "cookie":
                    var cookieName = apiKeyHeader ?? "api_key";
                    client.DefaultRequestHeaders.Add("Cookie", $"{cookieName}={apiKey}");
                    break;
                case "query":
                    break;
                default:
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
        string? apiKeyLocation = null,
        string? authUser = null,
        string? authPassword = null,
        string? clientId = null,
        string? clientSecret = null,
        string? tenant = null,
        string? agentCardUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) != string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("--client-id and --client-secret must be provided together.");
        }

        if (!string.IsNullOrEmpty(authToken) || !string.IsNullOrEmpty(apiKey)
            || !string.IsNullOrEmpty(authHeader) || !string.IsNullOrEmpty(authUser))
        {
            return CreateHttpClient(
                authToken: authToken,
                authHeader: authHeader,
                apiKey: apiKey,
                apiKeyHeader: apiKeyHeader,
                apiKeyLocation: apiKeyLocation,
                authUser: authUser,
                authPassword: authPassword);
        }

        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
        {
            var token = await AuthenticateClientCredentialsAsync(
                agentUrl,
                agentCardUrl,
                clientId,
                clientSecret,
                cancellationToken);
            return CreateHttpClient(authToken: token.AccessToken);
        }

        var store = new TokenStore();
        var storageKey = TokenStore.BuildStorageKey(agentUrl, tenant);
        var storedToken = await store.LoadTokenAsync(storageKey);
        if (storedToken != null)
        {
            if (!storedToken.IsExpired)
            {
                return CreateHttpClient(authToken: storedToken.AccessToken);
            }

            if (!string.IsNullOrEmpty(storedToken.RefreshToken))
            {
                var refreshed = await DeviceCodeFlow.RefreshTokenAsync(storedToken, cancellationToken: cancellationToken);
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

    private static async Task<TokenResult> AuthenticateClientCredentialsAsync(
        string agentUrl,
        string? agentCardUrl,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        var card = await LoadAgentCardAsync(agentUrl, agentCardUrl, httpClient, cancellationToken);
        var oauth2Scheme = card.SecuritySchemes?
            .Values
            .Where(scheme => scheme.SchemeCase == SecuritySchemeCase.OAuth2)
            .Select(scheme => scheme.OAuth2SecurityScheme)
            .FirstOrDefault(scheme => scheme?.Flows != null);

        if (oauth2Scheme == null)
        {
            throw new InvalidOperationException("No OAuth2 scheme found in agent card for client credentials.");
        }

        var token = await ClientCredentialsFlow.AuthenticateAsync(
            oauth2Scheme,
            clientId,
            clientSecret,
            httpClient: httpClient,
            cancellationToken: cancellationToken);
        return token ?? throw new InvalidOperationException("Client credentials authentication failed.");
    }

    private static async Task<AgentCard> LoadAgentCardAsync(
        string agentUrl,
        string? agentCardUrl,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(agentCardUrl))
        {
            var fullUri = new Uri(agentCardUrl);
            var baseUri = new Uri($"{fullUri.Scheme}://{fullUri.Authority}");
            var resolver = new A2ACardResolver(baseUri, httpClient, fullUri.PathAndQuery);
            return await resolver.GetAgentCardAsync(cancellationToken);
        }

        if (agentUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var fullUri = new Uri(agentUrl);
            var baseUri = new Uri($"{fullUri.Scheme}://{fullUri.Authority}");
            var resolver = new A2ACardResolver(baseUri, httpClient, fullUri.PathAndQuery);
            return await resolver.GetAgentCardAsync(cancellationToken);
        }

        var requestUri = new Uri(agentUrl.TrimEnd('/'));
        var wellKnownResolver = new A2ACardResolver(requestUri, httpClient, "/.well-known/agent-card.json");
        return await wellKnownResolver.GetAgentCardAsync(cancellationToken);
    }

    private sealed class ApiKeyQueryParameterHandler(string parameterName, string parameterValue) : DelegatingHandler(new HttpClientHandler())
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri != null)
            {
                request.RequestUri = AppendQueryParameter(request.RequestUri, parameterName, parameterValue);
            }

            return base.SendAsync(request, cancellationToken);
        }

        private static Uri AppendQueryParameter(Uri uri, string name, string value)
        {
            var builder = new UriBuilder(uri);
            var query = builder.Query.TrimStart('?');
            var parameter = $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}";
            builder.Query = string.IsNullOrEmpty(query)
                ? parameter
                : $"{query}&{parameter}";
            return builder.Uri;
        }
    }
}
