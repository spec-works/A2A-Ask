using SpecWorks.AiCatalog.Parsing;

namespace A2AAsk.Catalog;

/// <summary>
/// Resolves AI Catalog inputs into A2A agent candidates.
/// </summary>
public sealed class CatalogInputResolver
{
    private static readonly string[] s_a2aMediaTypes =
    [
        "application/a2a-agent-card+json",
        "application/vnd.a2a.agent-card+json"
    ];

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogInputResolver"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client used to fetch catalog documents.</param>
    public CatalogInputResolver(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Resolves a catalog reference into A2A agent candidates.
    /// </summary>
    /// <param name="catalogReference">A catalog URL, host, or <c>@@catalog</c> reference.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resolved A2A agent candidates.</returns>
    public async Task<IReadOnlyList<ResolvedCatalogAgent>> ResolveAgentsAsync(
        string catalogReference,
        CancellationToken cancellationToken = default)
    {
        var catalogDocumentUri = ResolveCatalogDocumentUri(catalogReference);
        using var response = await _httpClient.GetAsync(catalogDocumentUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var catalog = AiCatalogParser.Parse(stream);

        var agents = new List<ResolvedCatalogAgent>();
        foreach (var entry in catalog.Entries)
        {
            if (!IsA2AAgentCard(entry.MediaType) || string.IsNullOrWhiteSpace(entry.Url))
            {
                continue;
            }

            if (!Uri.TryCreate(catalogDocumentUri, entry.Url, out var agentCardUri))
            {
                throw new InvalidOperationException($"Catalog entry '{entry.Identifier}' has an invalid URL '{entry.Url}'.");
            }

            agents.Add(new ResolvedCatalogAgent
            {
                CatalogUrl = catalogDocumentUri.ToString(),
                EntryId = entry.Identifier,
                DisplayName = entry.DisplayName,
                Description = entry.Description,
                AgentCardUrl = agentCardUri.ToString(),
                Tags = entry.Tags?.ToArray() ?? Array.Empty<string>()
            });
        }

        return agents;
    }

    /// <summary>
    /// Resolves a single named agent from a catalog.
    /// </summary>
    /// <param name="catalogReference">A catalog URL, host, or <c>@@catalog</c> reference.</param>
    /// <param name="agentName">The requested agent name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The resolved catalog agent.</returns>
    public async Task<ResolvedCatalogAgent> ResolveAgentAsync(
        string catalogReference,
        string agentName,
        CancellationToken cancellationToken = default)
    {
        var agents = await ResolveAgentsAsync(catalogReference, cancellationToken);
        return SelectAgent(agents, agentName, catalogReference);
    }

    /// <summary>
    /// Converts a catalog reference into an absolute catalog document URI.
    /// </summary>
    /// <param name="catalogReference">A catalog URL, host, or <c>@@catalog</c> reference.</param>
    /// <returns>The absolute catalog document URI.</returns>
    public static Uri ResolveCatalogDocumentUri(string catalogReference)
    {
        if (string.IsNullOrWhiteSpace(catalogReference))
        {
            throw new ArgumentException("Catalog reference must not be empty.", nameof(catalogReference));
        }

        var trimmed = catalogReference.Trim();
        if (trimmed.StartsWith("@@", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }

        Uri baseUri;
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri))
        {
            baseUri = absoluteUri;
        }
        else
        {
            var scheme = IsLocalHostAlias(trimmed) ? Uri.UriSchemeHttp : Uri.UriSchemeHttps;
            baseUri = new Uri($"{scheme}://{trimmed}");
        }

        if (baseUri.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return baseUri;
        }

        var builder = new UriBuilder(baseUri)
        {
            Path = string.IsNullOrEmpty(baseUri.AbsolutePath.Trim('/'))
                ? "/.well-known/ai-catalog.json"
                : $"{baseUri.AbsolutePath.TrimEnd('/')}/.well-known/ai-catalog.json"
        };
        return builder.Uri;
    }

    /// <summary>
    /// Selects a single agent from a set of catalog candidates.
    /// </summary>
    /// <param name="agents">The catalog candidates.</param>
    /// <param name="agentName">The requested agent name.</param>
    /// <param name="catalogReference">The catalog reference used for hint text.</param>
    /// <returns>The selected agent.</returns>
    public static ResolvedCatalogAgent SelectAgent(
        IReadOnlyList<ResolvedCatalogAgent> agents,
        string agentName,
        string catalogReference)
    {
        if (agents.Count == 0)
        {
            throw new InvalidOperationException($"No A2A agents were found in catalog '{catalogReference}'.");
        }

        var matches = FindMatchingAgents(agents, agentName);
        return matches.Count switch
        {
            1 => matches[0],
            > 1 => throw CreateAmbiguousAgentException(agentName, catalogReference, matches),
            _ => throw new InvalidOperationException(
                $"No A2A agent matching '{agentName}' was found in catalog '{catalogReference}'. Available agents: {FormatCandidates(agents)}")
        };
    }

    internal static IReadOnlyList<ResolvedCatalogAgent> FindMatchingAgents(
        IReadOnlyList<ResolvedCatalogAgent> agents,
        string agentName)
    {
        var exactIdentifierMatches = agents
            .Where(agent => string.Equals(agent.EntryId, agentName, StringComparison.Ordinal))
            .ToList();
        if (exactIdentifierMatches.Count > 0)
        {
            return exactIdentifierMatches;
        }

        var exactDisplayNameMatches = agents
            .Where(agent => string.Equals(agent.DisplayName, agentName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exactDisplayNameMatches.Count > 0)
        {
            return exactDisplayNameMatches;
        }

        var exactTagMatches = agents
            .Where(agent => agent.Tags.Any(tag => string.Equals(tag, agentName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (exactTagMatches.Count > 0)
        {
            return exactTagMatches;
        }

        return agents
            .Where(agent =>
                agent.EntryId.Contains(agentName, StringComparison.OrdinalIgnoreCase)
                || agent.DisplayName.Contains(agentName, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(agent.Description)
                    && agent.Description.Contains(agentName, StringComparison.OrdinalIgnoreCase))
                || agent.Tags.Any(tag => tag.Contains(agentName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Returns whether the supplied media type denotes an A2A agent card.
    /// </summary>
    /// <param name="mediaType">The media type to inspect.</param>
    /// <returns><see langword="true"/> when the media type denotes an A2A agent card; otherwise <see langword="false"/>.</returns>
    public static bool IsA2AAgentCard(string? mediaType) =>
        !string.IsNullOrWhiteSpace(mediaType)
        && s_a2aMediaTypes.Contains(mediaType, StringComparer.OrdinalIgnoreCase);

    private static bool IsLocalHostAlias(string catalogAlias) =>
        catalogAlias.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
        || catalogAlias.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        || catalogAlias.StartsWith("[::1]", StringComparison.OrdinalIgnoreCase);

    private static InvalidOperationException CreateAmbiguousAgentException(
        string agentName,
        string catalogReference,
        IReadOnlyList<ResolvedCatalogAgent> matches) =>
        new($"Multiple A2A agents matched '{agentName}' in catalog '{catalogReference}': {FormatCandidates(matches)}. Use <agent>@{catalogReference} to pick one explicitly.");

    private static string FormatCandidates(IReadOnlyList<ResolvedCatalogAgent> agents) =>
        string.Join(", ",
            agents.Select(agent => string.IsNullOrWhiteSpace(agent.DisplayName)
                ? agent.EntryId
                : $"{agent.EntryId} ({agent.DisplayName})"));
}
