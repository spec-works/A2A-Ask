using System.CommandLine;
using A2A;
using A2AAsk.Catalog;
using V03Compat = A2A.V0_3Compat;

namespace A2AAsk.Commands;

/// <summary>
/// Common CLI options shared across multiple commands.
/// </summary>
public static class CommonOptions
{
    public static Option<string?> AuthToken() => new(
        name: "--auth-token",
        description: "Bearer token for authentication");

    public static Option<string?> AuthHeader() => new(
        name: "--auth-header",
        description: "Custom auth header (key=value format)");

    public static Option<string?> ApiKey() => new(
        name: "--api-key",
        description: "API key value");

    public static Option<string?> ApiKeyHeader() => new(
        name: "--api-key-header",
        description: "API key header name (defaults to agent card setting)");

    public static Option<string?> ApiKeyLocation() => new(
        name: "--api-key-location",
        description: "API key location: header (default), query, or cookie");

    public static Option<string?> AuthUser() => new(
        name: "--auth-user",
        description: "Username for HTTP Basic authentication");

    public static Option<string?> AuthPassword() => new(
        name: "--auth-password",
        description: "Password for HTTP Basic authentication");

    public static Option<string?> ClientId() => new(
        name: "--client-id",
        description: "OAuth2 client ID for client_credentials grant");

    public static Option<string?> ClientSecret() => new(
        name: "--client-secret",
        description: "OAuth2 client secret for client_credentials grant");

    public static Option<string?> TaskId() => new(
        aliases: ["--task-id", "-t"],
        description: "Task ID for continuing an existing task");

    public static Option<string?> ContextId() => new(
        aliases: ["--context-id", "-c"],
        description: "Context ID for grouping related interactions");

    public static Option<string> Binding() => new(
        name: "--binding",
        description: "Protocol binding: auto, http, jsonrpc",
        getDefaultValue: () => "auto");

    public static Option<string> A2AVersion() => new(
        name: "--a2a-version",
        description: "A2A protocol version",
        getDefaultValue: () => "1.0");

    public static Option<string?> Tenant() => new(
        name: "--tenant",
        description: "Tenant ID");

    public static Option<string?> SaveArtifacts() => new(
        name: "--save-artifacts",
        description: "Directory to save file artifacts to disk");

    internal static async Task<ResolvedTarget> ResolveTargetAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        using var catalogHttpClient = new HttpClient();
        return await ResolveTargetAsync(target, catalogHttpClient, cancellationToken);
    }

    internal static async Task<ResolvedTarget> ResolveTargetAsync(
        string target,
        HttpClient catalogHttpClient,
        CancellationToken cancellationToken = default)
    {
        var parsedTarget = TargetParser.Parse(target);
        var resolver = new CatalogInputResolver(catalogHttpClient);

        if (parsedTarget is CatalogTarget catalogTarget)
        {
            if (string.IsNullOrWhiteSpace(catalogTarget.CatalogAlias))
            {
                throw new InvalidOperationException("Phase 1 requires a catalog host or URL. Use @agent@catalog or a catalog URL.");
            }

            var agent = await resolver.ResolveAgentAsync(catalogTarget.CatalogAlias!, catalogTarget.AgentName, cancellationToken);
            return ResolvedTarget.FromCatalogAgent(agent);
        }

        if (parsedTarget is CatalogBrowse catalogBrowse)
        {
            var agents = await resolver.ResolveAgentsAsync(catalogBrowse.CatalogAlias, cancellationToken);
            return ResolvedTarget.FromCatalogAgent(SelectSingleCatalogAgent(agents, catalogBrowse.CatalogAlias));
        }

        var directUrl = ((DirectUrl)parsedTarget).Url;
        var shouldTryCatalog = TargetParser.IsOriginOnlyUrl(directUrl, out _)
            || directUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        if (!shouldTryCatalog)
        {
            return new ResolvedTarget { RequestUrl = directUrl };
        }

        try
        {
            var agents = await resolver.ResolveAgentsAsync(directUrl, cancellationToken);
            return agents.Count switch
            {
                0 => new ResolvedTarget { RequestUrl = directUrl },
                1 => ResolvedTarget.FromCatalogAgent(agents[0]),
                _ => throw new InvalidOperationException(
                    $"Multiple A2A agents were found in catalog '{directUrl}': {FormatCatalogCandidates(agents)}. Use @<agent>@{directUrl} or `a2a-ask catalog list {directUrl}`.")
            };
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            return new ResolvedTarget { RequestUrl = directUrl };
        }
    }

    /// <summary>
    /// Creates an <see cref="IA2AClient"/> that sends requests directly to the provided URL.
    /// </summary>
    public static Task<IA2AClient> CreateClientAsync(
        string url,
        HttpClient httpClient,
        CancellationToken cancellationToken = default) =>
        CreateClientAsync(url, httpClient, "1.0", cancellationToken);

    public static Task<IA2AClient> CreateClientAsync(
        string url,
        HttpClient httpClient,
        string a2aVersion,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateDirectClient(url, httpClient, a2aVersion));
    }

    internal static Task<IA2AClient> CreateClientAsync(
        ResolvedTarget target,
        HttpClient httpClient,
        CancellationToken cancellationToken = default) =>
        CreateClientAsync(target, httpClient, "1.0", cancellationToken);

    internal static async Task<IA2AClient> CreateClientAsync(
        ResolvedTarget target,
        HttpClient httpClient,
        string a2aVersion = "1.0",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target.AgentCardUrl))
        {
            return await CreateClientAsync(target.RequestUrl, httpClient, a2aVersion, cancellationToken);
        }

        var cardUri = new Uri(target.AgentCardUrl);
        if (IsV03(a2aVersion))
        {
            var baseUri = new Uri($"{cardUri.Scheme}://{cardUri.Authority}");
            return await V03Compat.V03CompatClientFactory.CreateAsync(
                baseUri,
                httpClient,
                cardUri.PathAndQuery,
                cancellationToken);
        }

        var resolver = new A2ACardResolver(
            new Uri($"{cardUri.Scheme}://{cardUri.Authority}"),
            httpClient,
            cardUri.PathAndQuery);
        var card = await resolver.GetAgentCardAsync(cancellationToken);
        return A2AClientFactory.Create(card, httpClient);
    }

    internal static bool IsV03(string a2aVersion) =>
        a2aVersion.StartsWith("0.3", StringComparison.OrdinalIgnoreCase);

    private static IA2AClient CreateDirectClient(string url, HttpClient httpClient, string a2aVersion)
    {
        var requestUri = new Uri(url, UriKind.Absolute);
        return IsV03(a2aVersion)
            ? V03Compat.V03CompatClientFactory.Create(requestUri, httpClient)
            : new A2AClient(requestUri, httpClient);
    }

    private static ResolvedCatalogAgent SelectSingleCatalogAgent(
        IReadOnlyList<ResolvedCatalogAgent> agents,
        string catalogReference) => agents.Count switch
    {
        0 => throw new InvalidOperationException($"No A2A agents were found in catalog '{catalogReference}'."),
        1 => agents[0],
        _ => throw new InvalidOperationException(
            $"Multiple A2A agents were found in catalog '{catalogReference}': {FormatCatalogCandidates(agents)}. Use @<agent>@{catalogReference} or `a2a-ask catalog list {catalogReference}`.")
    };

    private static string FormatCatalogCandidates(IReadOnlyList<ResolvedCatalogAgent> agents) =>
        string.Join(", ", agents.Select(agent => $"{agent.EntryId} ({agent.DisplayName})"));

    internal sealed record ResolvedTarget
    {
        public required string RequestUrl { get; init; }

        public string? AgentCardUrl { get; init; }

        public static ResolvedTarget FromCatalogAgent(ResolvedCatalogAgent agent)
        {
            var cardUri = new Uri(agent.AgentCardUrl);
            const string wellKnownSuffix = "/.well-known/agent-card.json";
            var requestUrl = cardUri.AbsolutePath.EndsWith(wellKnownSuffix, StringComparison.OrdinalIgnoreCase)
                ? new UriBuilder(cardUri)
                {
                    Path = cardUri.AbsolutePath[..cardUri.AbsolutePath.LastIndexOf(wellKnownSuffix, StringComparison.OrdinalIgnoreCase)],
                    Query = string.Empty
                }.Uri.ToString().TrimEnd('/')
                : agent.AgentCardUrl;

            if (string.IsNullOrWhiteSpace(requestUrl))
            {
                requestUrl = $"{cardUri.Scheme}://{cardUri.Authority}";
            }

            return new ResolvedTarget
            {
                RequestUrl = string.IsNullOrWhiteSpace(requestUrl)
                    ? $"{cardUri.Scheme}://{cardUri.Authority}"
                    : requestUrl,
                AgentCardUrl = agent.AgentCardUrl
            };
        }
    }
}
