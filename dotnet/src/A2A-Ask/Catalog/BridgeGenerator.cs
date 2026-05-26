using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace A2AAsk.Catalog;

public sealed record BridgeAgentSkill(string Id, string Name, string? Description);

public sealed record BridgeAgentCard(
    string Name,
    string? Description,
    IReadOnlyList<BridgeAgentSkill> Skills,
    bool RequiresAuthentication,
    bool SupportsStreaming);

public sealed record BridgeRemoteAgentMetadata(
    string Catalog,
    string EntryId,
    string CardUrl,
    string? CardEtag,
    string? CardHash,
    string InstalledAt);

public sealed record BridgeTemplateModel(
    string Name,
    string CatalogTarget,
    string DisplayName,
    BridgeAgentCard Card,
    BridgeRemoteAgentMetadata RemoteAgent);

public sealed class BridgeGenerator
{
    public const string BeginGeneratedMarker = "<!-- a2a:begin-generated -->";
    public const string EndGeneratedMarker = "<!-- a2a:end-generated -->";

    private static readonly HashSet<string> s_reservedNames =
    [
        "explore",
        "task",
        "code-review",
        "general-purpose",
        "research",
        "rubber-duck"
    ];

    public string GenerateMarkdown(BridgeTemplateModel model)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"name: {model.Name}");
        builder.AppendLine($"description: {QuoteYaml(BuildBridgeDescription(model.Card))}");
        builder.AppendLine("tools: ['shell']");
        foreach (var line in GenerateRemoteAgentBlockLines(model.RemoteAgent))
        {
            builder.AppendLine(line);
        }

        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine(GenerateGeneratedSection(model));
        builder.AppendLine();
        builder.AppendLine("<!-- Anything below this line is preserved across `a2a-ask catalog sync`. -->");
        return builder.ToString();
    }

    public string GenerateGeneratedSection(BridgeTemplateModel model)
    {
        var builder = new StringBuilder();
        builder.AppendLine(BeginGeneratedMarker);
        builder.AppendLine($"# {model.DisplayName} (A2A bridge)");
        builder.AppendLine();
        builder.AppendLine($"You are a thin bridge to the **{model.Card.Name}** A2A agent at");
        builder.AppendLine($"`{model.CatalogTarget}`. You do not answer the user's request yourself —");
        builder.AppendLine("you forward it and relay the response.");
        builder.AppendLine();
        builder.AppendLine("## Skills declared by the agent card");
        builder.AppendLine();

        if (model.Card.Skills.Count == 0)
        {
            builder.AppendLine("- None declared.");
        }
        else
        {
            foreach (var skill in model.Card.Skills)
            {
                var description = string.IsNullOrWhiteSpace(skill.Description)
                    ? "No description provided."
                    : ToSingleLine(skill.Description);
                builder.AppendLine($"- `{skill.Id}` — {description}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Your job");
        builder.AppendLine();
        builder.AppendLine("1. If `{{task_id}}` is empty, start a new task:");
        builder.AppendLine("   ```powershell");
        builder.AppendLine($"   a2a-ask send \"{model.CatalogTarget}\" --message \"{{{{request}}}}\" --output json");
        builder.AppendLine("   ```");
        builder.AppendLine("   Otherwise, continue the existing task:");
        builder.AppendLine("   ```powershell");
        builder.AppendLine($"   a2a-ask send \"{model.CatalogTarget}\" --task-id \"{{{{task_id}}}}\" --message \"{{{{request}}}}\" --output json");
        builder.AppendLine("   ```");
        builder.AppendLine();
        builder.AppendLine("2. Record the `taskId` from the response in your working memory.");
        builder.AppendLine("   Reuse it on every follow-up turn within this Copilot sub-agent task.");
        builder.AppendLine();
        builder.AppendLine("3. Interpret the terminal state:");
        builder.AppendLine("   - **Completed** → relay the agent's final message verbatim. Exit `completed`.");
        builder.AppendLine("   - **InputRequired** → relay the agent's question; exit `input_required`.");
        builder.AppendLine($"   - **AuthRequired** → tell the user to run");
        builder.AppendLine($"     `a2a-ask auth login \"{model.CatalogTarget}\"`; exit `auth_required`.");
        builder.AppendLine("   - **Failed / Canceled / Rejected** → relay the reason; exit `failed`.");
        builder.AppendLine();
        builder.AppendLine("4. For long-running or streaming work, prefer:");
        builder.AppendLine("   ```powershell");
        builder.AppendLine($"   a2a-ask stream \"{model.CatalogTarget}\" --message \"{{{{request}}}}\" --output text");
        builder.AppendLine("   ```");
        builder.AppendLine();
        builder.AppendLine("## Rules");
        builder.AppendLine();
        builder.AppendLine("- Never fabricate output. If the agent fails, say so.");
        builder.AppendLine("- Do not call any tool other than `a2a-ask`.");
        builder.AppendLine("- Preserve the agent's wording — you are transport, not editor.");
        builder.AppendLine("- If the user changes topic away from this agent's stated skills,");
        builder.AppendLine("  decline and stop.");
        builder.Append(EndGeneratedMarker);
        return builder.ToString();
    }

    public static BridgeAgentCard ParseAgentCard(JsonElement root)
    {
        var name = root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Agent card is missing a 'name' property.");
        }

        var description = root.TryGetProperty("description", out var descriptionElement) && descriptionElement.ValueKind == JsonValueKind.String
            ? descriptionElement.GetString()
            : null;

        var skills = new List<BridgeAgentSkill>();
        if (root.TryGetProperty("skills", out var skillsElement) && skillsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var skill in skillsElement.EnumerateArray())
            {
                if (skill.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = skill.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
                    ? idElement.GetString()
                    : null;
                var skillName = skill.TryGetProperty("name", out var skillNameElement) && skillNameElement.ValueKind == JsonValueKind.String
                    ? skillNameElement.GetString()
                    : null;
                var skillDescription = skill.TryGetProperty("description", out var skillDescriptionElement) && skillDescriptionElement.ValueKind == JsonValueKind.String
                    ? skillDescriptionElement.GetString()
                    : null;

                var resolvedId = string.IsNullOrWhiteSpace(id)
                    ? GenerateKebabCaseName(skillName ?? "skill")
                    : id.Trim();
                var resolvedName = string.IsNullOrWhiteSpace(skillName)
                    ? resolvedId
                    : skillName.Trim();

                skills.Add(new BridgeAgentSkill(resolvedId, resolvedName, skillDescription));
            }
        }

        var requiresAuthentication = root.TryGetProperty("securitySchemes", out var securitySchemes)
            && securitySchemes.ValueKind == JsonValueKind.Object
            && securitySchemes.EnumerateObject().Any();
        var supportsStreaming = root.TryGetProperty("capabilities", out var capabilities)
            && capabilities.ValueKind == JsonValueKind.Object
            && capabilities.TryGetProperty("streaming", out var streaming)
            && streaming.ValueKind is JsonValueKind.True or JsonValueKind.False
            && streaming.GetBoolean();

        return new BridgeAgentCard(name.Trim(), description?.Trim(), skills, requiresAuthentication, supportsStreaming);
    }

    public static string GenerateKebabCaseName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "a2a-agent";
        }

        var builder = new StringBuilder();
        var previousWasSeparator = false;

        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator || builder.Length == 0)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        var result = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "a2a-agent" : result;
    }

    public static bool IsReservedName(string name) => s_reservedNames.Contains(name);

    public static string ComputeSha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    public static IReadOnlyList<string> GenerateRemoteAgentBlockLines(BridgeRemoteAgentMetadata metadata)
    {
        var lines = new List<string>
        {
            "remote-agent:",
            $"  catalog: {QuoteYaml(metadata.Catalog)}",
            $"  entry-id: {QuoteYaml(metadata.EntryId)}",
            $"  card-url: {QuoteYaml(metadata.CardUrl)}"
        };

        if (!string.IsNullOrWhiteSpace(metadata.CardEtag))
        {
            lines.Add($"  card-etag: {QuoteYaml(metadata.CardEtag)}");
        }

        if (!string.IsNullOrWhiteSpace(metadata.CardHash))
        {
            lines.Add($"  card-hash: {QuoteYaml(metadata.CardHash)}");
        }

        lines.Add($"  installed-at: {QuoteYaml(metadata.InstalledAt)}");
        return lines;
    }

    public static string ReplaceGeneratedSection(string markdown, string generatedSection)
    {
        var beginIndex = markdown.IndexOf(BeginGeneratedMarker, StringComparison.Ordinal);
        var endIndex = markdown.IndexOf(EndGeneratedMarker, StringComparison.Ordinal);
        if (beginIndex < 0 || endIndex < beginIndex)
        {
            throw new InvalidOperationException("The bridge file is missing generated content markers.");
        }

        var replacementStart = markdown.LastIndexOf('\n', beginIndex);
        replacementStart = replacementStart < 0 ? 0 : replacementStart + 1;

        var replacementEnd = endIndex + EndGeneratedMarker.Length;
        if (replacementEnd < markdown.Length)
        {
            if (markdown[replacementEnd] == '\r')
            {
                replacementEnd++;
            }

            if (replacementEnd < markdown.Length && markdown[replacementEnd] == '\n')
            {
                replacementEnd++;
            }
        }

        return string.Concat(markdown.AsSpan(0, replacementStart), generatedSection, Environment.NewLine, markdown.AsSpan(replacementEnd));
    }

    private static string BuildBridgeDescription(BridgeAgentCard card)
    {
        var description = ToSingleLine(card.Description);
        if (string.IsNullOrWhiteSpace(description))
        {
            description = $"Bridge to the {card.Name} A2A agent";
        }

        var skillNames = card.Skills
            .Select(skill => string.IsNullOrWhiteSpace(skill.Name) ? skill.Id : skill.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (skillNames.Count == 0)
        {
            return description;
        }

        return description.EndsWith(".", StringComparison.Ordinal)
            ? $"{description} Skills: {string.Join(", ", skillNames)}."
            : $"{description}. Skills: {string.Join(", ", skillNames)}.";
    }

    private static string ToSingleLine(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : string.Join(" ", value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string QuoteYaml(string value) => $"'{value.Replace("'", "''")}'";
}
