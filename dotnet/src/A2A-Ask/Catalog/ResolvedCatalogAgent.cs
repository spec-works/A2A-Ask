namespace A2AAsk.Catalog;

/// <summary>
/// Represents a catalog entry resolved to an A2A agent card.
/// </summary>
public sealed record ResolvedCatalogAgent
{
    /// <summary>
    /// Gets the absolute URL of the catalog document that contained the entry.
    /// </summary>
    public required string CatalogUrl { get; init; }

    /// <summary>
    /// Gets the catalog entry identifier.
    /// </summary>
    public required string EntryId { get; init; }

    /// <summary>
    /// Gets the human-readable display name for the agent.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the optional catalog entry description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the absolute URL of the referenced agent card.
    /// </summary>
    public required string AgentCardUrl { get; init; }

    /// <summary>
    /// Gets the tags associated with the catalog entry.
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
