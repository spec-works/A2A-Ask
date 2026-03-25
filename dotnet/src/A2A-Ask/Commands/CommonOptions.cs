using System.CommandLine;
using A2A;

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

    /// <summary>
    /// Creates an IA2AClient via the factory, using the agent URL to derive the
    /// well-known agent card path. Tries the agent URL itself first (future spec),
    /// then falls back to {path}/.well-known/agent-card.json.
    /// </summary>
    public static async Task<IA2AClient> CreateClientAsync(
        string url, HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        var agentUri = new Uri(url.TrimEnd('/') + "/");

        // Try GET on the agent URL itself — future spec direction
        try
        {
            using var probeRequest = new HttpRequestMessage(HttpMethod.Get, url);
            probeRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            probeRequest.Headers.TryAddWithoutValidation("A2A-Version", "1.0");

            using var probeResponse = await httpClient.SendAsync(probeRequest, cancellationToken);
            if (probeResponse.IsSuccessStatusCode)
            {
                var json = await probeResponse.Content.ReadAsStringAsync(cancellationToken);
                // Quick check: does this look like an agent card (has "name" property)?
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("name", out _))
                {
                    return A2AClientFactory.Create(json, agentUri, httpClient);
                }
            }
        }
        catch
        {
            // Probe failed — fall through to well-known path
        }

        // Fall back to {path}/.well-known/agent-card.json
        var cardPath = agentUri.AbsolutePath + ".well-known/agent-card.json";
        var options = new A2AClientOptions
        {
            AgentCardPath = cardPath
        };

        return await A2AClientFactory.CreateAsync(agentUri, httpClient, options, cancellationToken);
    }
}
