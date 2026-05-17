namespace A2AAsk.Catalog;

/// <summary>
/// Base type for parsed CLI targets.
/// </summary>
public abstract record TargetParseResult;

/// <summary>
/// Represents a direct URL target.
/// </summary>
public sealed record DirectUrl : TargetParseResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DirectUrl"/> record.
    /// </summary>
    /// <param name="url">The direct URL value.</param>
    public DirectUrl(string url)
    {
        Url = url;
    }

    /// <summary>
    /// Gets the direct URL value.
    /// </summary>
    public string Url { get; init; }
}

/// <summary>
/// Represents a request to resolve an agent from a catalog.
/// </summary>
public sealed record CatalogTarget : TargetParseResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogTarget"/> record.
    /// </summary>
    /// <param name="agentName">The requested agent name.</param>
    /// <param name="catalogAlias">The optional catalog alias or host hint.</param>
    public CatalogTarget(string agentName, string? catalogAlias)
    {
        AgentName = agentName;
        CatalogAlias = catalogAlias;
    }

    /// <summary>
    /// Gets the requested agent name.
    /// </summary>
    public string AgentName { get; init; }

    /// <summary>
    /// Gets the optional catalog alias or host hint.
    /// </summary>
    public string? CatalogAlias { get; init; }
}

/// <summary>
/// Represents a request to browse a catalog.
/// </summary>
public sealed record CatalogBrowse : TargetParseResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CatalogBrowse"/> record.
    /// </summary>
    /// <param name="catalogAlias">The catalog alias or host hint.</param>
    public CatalogBrowse(string catalogAlias)
    {
        CatalogAlias = catalogAlias;
    }

    /// <summary>
    /// Gets the catalog alias or host hint.
    /// </summary>
    public string CatalogAlias { get; init; }
}

/// <summary>
/// Parses CLI target values into direct URL or catalog-addressed forms.
/// </summary>
public static class TargetParser
{
    /// <summary>
    /// Parses a CLI target value.
    /// </summary>
    /// <param name="target">The raw CLI target.</param>
    /// <returns>A parsed target representation.</returns>
    public static TargetParseResult Parse(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new ArgumentException("Target must not be empty.", nameof(target));
        }

        var trimmed = target.Trim();
        if (!trimmed.StartsWith('@'))
        {
            return new DirectUrl(trimmed);
        }

        if (trimmed.StartsWith("@@", StringComparison.Ordinal))
        {
            var catalogAlias = trimmed[2..].Trim();
            if (string.IsNullOrWhiteSpace(catalogAlias))
            {
                throw new ArgumentException("Catalog browse targets must include a catalog alias or host.", nameof(target));
            }

            return new CatalogBrowse(catalogAlias);
        }

        var separatorIndex = trimmed.IndexOf('@', 1);
        if (separatorIndex < 0)
        {
            var agentName = trimmed[1..].Trim();
            if (string.IsNullOrWhiteSpace(agentName))
            {
                throw new ArgumentException("Catalog agent targets must include an agent name.", nameof(target));
            }

            return new CatalogTarget(agentName, null);
        }

        var name = trimmed[1..separatorIndex].Trim();
        var catalog = trimmed[(separatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(catalog))
        {
            throw new ArgumentException("Catalog agent targets must use the form @agent@catalog.", nameof(target));
        }

        return new CatalogTarget(name, catalog);
    }

    /// <summary>
    /// Determines whether a direct URL points to an origin root.
    /// </summary>
    /// <param name="target">The direct target value.</param>
    /// <param name="uri">The parsed absolute URI when available.</param>
    /// <returns><see langword="true"/> when the URL is an origin-only HTTP(S) URL; otherwise <see langword="false"/>.</returns>
    public static bool IsOriginOnlyUrl(string target, out Uri? uri)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            uri = null;
            return false;
        }

        uri = parsed;
        return string.IsNullOrEmpty(parsed.AbsolutePath.Trim('/'))
            && string.IsNullOrEmpty(parsed.Query)
            && string.IsNullOrEmpty(parsed.Fragment);
    }
}
