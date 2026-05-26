namespace A2AAsk.Catalog;

public sealed record RemoteAgentFrontmatter(
    string? Name,
    string Catalog,
    string EntryId,
    string CardUrl,
    string? CardEtag,
    string? CardHash,
    string? InstalledAt,
    int FrontmatterStartLineIndex,
    int FrontmatterEndLineIndex,
    int RemoteAgentStartLineIndex,
    int RemoteAgentEndLineIndex);

public static class FrontmatterReader
{
    public static bool TryRead(string markdown, out RemoteAgentFrontmatter? frontmatter)
    {
        frontmatter = null;
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return false;
        }

        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length < 3 || lines[0].Trim() != "---")
        {
            return false;
        }

        var frontmatterEnd = Array.FindIndex(lines, 1, line => line.Trim() == "---");
        if (frontmatterEnd < 0)
        {
            return false;
        }

        string? name = null;
        string? catalog = null;
        string? entryId = null;
        string? cardUrl = null;
        string? cardEtag = null;
        string? cardHash = null;
        string? installedAt = null;
        int? remoteAgentStart = null;
        int? remoteAgentEnd = null;

        for (var index = 1; index < frontmatterEnd; index++)
        {
            var line = lines[index];
            if (line.StartsWith("remote-agent:", StringComparison.Ordinal))
            {
                remoteAgentStart = index;
                remoteAgentEnd = index;

                for (var nestedIndex = index + 1; nestedIndex < frontmatterEnd; nestedIndex++)
                {
                    var nestedLine = lines[nestedIndex];
                    if (!nestedLine.StartsWith("  ", StringComparison.Ordinal))
                    {
                        break;
                    }

                    remoteAgentEnd = nestedIndex;
                    var (key, value) = ParseLine(nestedLine.Trim());
                    switch (key)
                    {
                        case "catalog":
                            catalog = value;
                            break;
                        case "entry-id":
                            entryId = value;
                            break;
                        case "card-url":
                            cardUrl = value;
                            break;
                        case "card-etag":
                            cardEtag = value;
                            break;
                        case "card-hash":
                            cardHash = value;
                            break;
                        case "installed-at":
                            installedAt = value;
                            break;
                    }
                }

                continue;
            }

            if (char.IsWhiteSpace(line.FirstOrDefault()))
            {
                continue;
            }

            var (topLevelKey, topLevelValue) = ParseLine(line.Trim());
            if (string.Equals(topLevelKey, "name", StringComparison.Ordinal))
            {
                name = topLevelValue;
            }
        }

        if (remoteAgentStart is null || remoteAgentEnd is null
            || string.IsNullOrWhiteSpace(catalog)
            || string.IsNullOrWhiteSpace(entryId)
            || string.IsNullOrWhiteSpace(cardUrl))
        {
            return false;
        }

        frontmatter = new RemoteAgentFrontmatter(
            name,
            catalog,
            entryId,
            cardUrl,
            cardEtag,
            cardHash,
            installedAt,
            0,
            frontmatterEnd,
            remoteAgentStart.Value,
            remoteAgentEnd.Value);
        return true;
    }

    private static (string Key, string? Value) ParseLine(string line)
    {
        var separatorIndex = line.IndexOf(':');
        if (separatorIndex < 0)
        {
            return (line, null);
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();
        return (key, Unquote(value));
    }

    private static string? Unquote(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length >= 2)
        {
            if (value[0] == '\'' && value[^1] == '\'')
            {
                return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
            }

            if (value[0] == '"' && value[^1] == '"')
            {
                return value[1..^1];
            }
        }

        return value;
    }
}
