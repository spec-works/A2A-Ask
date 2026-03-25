using System.Text.Json;
using System.Text.Json.Serialization;
using A2A;

namespace A2AAsk.Output;

/// <summary>
/// Formats A2A responses for console output in JSON or text mode.
/// </summary>
public class ConsoleFormatter
{
    private readonly string _mode;
    private readonly bool _pretty;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConsoleFormatter(string mode, bool pretty)
    {
        _mode = mode;
        _pretty = pretty;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = pretty,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
    }

    public void WriteAgentCard(AgentCard card, bool verbose)
    {
        if (_mode == "text")
            WriteAgentCardText(card, verbose);
        else
            WriteJson(card);
    }

    public void WriteResponse(SendMessageResponse response, bool verbose)
    {
        if (_mode == "text")
            WriteResponseText(response, verbose);
        else
            WriteJson(response);
    }

    public void WriteTask(AgentTask task, bool verbose)
    {
        if (_mode == "text")
            WriteTaskText(task, verbose);
        else
            WriteJson(task);
    }

    public void WriteListTasksNotSupported()
    {
        if (_mode == "text")
            Console.WriteLine("The 'task list' feature is not available in this version of the A2A SDK.");
        else
            WriteJson(new { error = "task list not supported in current A2A SDK" });
    }

    public void WriteStreamEvent(StreamResponse evt)
    {
        WriteJson(evt);
    }

    public void WriteJson<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj, _jsonOptions);
        Console.WriteLine(json);
    }

    public static void WriteError(Exception ex, bool verbose)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        if (verbose && ex.InnerException != null)
            Console.Error.WriteLine($"  Inner: {ex.InnerException.Message}");
        if (verbose)
        {
            Console.Error.WriteLine($"  Type: {ex.GetType().Name}");
            Console.Error.WriteLine($"  Stack: {ex.StackTrace}");
        }
    }

    #region Text Mode Formatters

    private void WriteAgentCardText(AgentCard card, bool verbose)
    {
        Console.WriteLine($"Agent: {card.Name}");
        Console.WriteLine($"Description: {card.Description}");
        Console.WriteLine($"Version: {card.Version}");

        if (card.Provider != null)
            Console.WriteLine($"Provider: {card.Provider.Organization} ({card.Provider.Url})");

        if (card.DocumentationUrl != null)
            Console.WriteLine($"Documentation: {card.DocumentationUrl}");

        Console.WriteLine();
        Console.WriteLine("Capabilities:");
        if (card.Capabilities != null)
        {
            Console.WriteLine($"  Streaming: {card.Capabilities.Streaming ?? false}");
            Console.WriteLine($"  Push Notifications: {card.Capabilities.PushNotifications ?? false}");
        }

        if (card.SupportedInterfaces != null && card.SupportedInterfaces.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Supported Interfaces:");
            foreach (var iface in card.SupportedInterfaces)
                Console.WriteLine($"  {iface.ProtocolBinding} -> {iface.Url}");
        }

        Console.WriteLine();
        Console.WriteLine($"Input Modes: {FormatList(card.DefaultInputModes)}");
        Console.WriteLine($"Output Modes: {FormatList(card.DefaultOutputModes)}");

        Console.WriteLine();
        Console.WriteLine("Skills:");
        if (card.Skills != null)
        {
            foreach (var skill in card.Skills)
            {
                Console.WriteLine($"  [{skill.Id}] {skill.Name}");
                Console.WriteLine($"    {skill.Description}");
                if (skill.Tags != null && skill.Tags.Count > 0)
                    Console.WriteLine($"    Tags: {string.Join(", ", skill.Tags)}");
                if (verbose && skill.Examples != null && skill.Examples.Count > 0)
                {
                    Console.WriteLine("    Examples:");
                    foreach (var ex in skill.Examples)
                        Console.WriteLine($"      - {ex}");
                }
            }
        }

        if (card.SecuritySchemes != null && card.SecuritySchemes.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Security Schemes:");
            foreach (var (name, scheme) in card.SecuritySchemes)
                Console.WriteLine($"  \"{name}\": {GetSchemeDescription(scheme)}");

            if (card.SecurityRequirements != null && card.SecurityRequirements.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Security Requirements:");
                foreach (var req in card.SecurityRequirements)
                {
                    foreach (var (schemeName, scopes) in req.Schemes!)
                    {
                        var scopeStr = scopes.List.Count > 0 ? $" [{string.Join(", ", scopes.List)}]" : "";
                        Console.WriteLine($"  - {schemeName}{scopeStr}");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Warning: This agent requires authentication. Use one of:");
            foreach (var (name, scheme) in card.SecuritySchemes)
            {
                if (scheme.SchemeCase == SecuritySchemeCase.HttpAuth)
                {
                    var http = scheme.HttpAuthSecurityScheme!;
                    Console.WriteLine($"  --auth-token <your-{http.Scheme?.ToLower() ?? "bearer"}-token>");
                }
                else if (scheme.SchemeCase == SecuritySchemeCase.ApiKey)
                {
                    var apiKey = scheme.ApiKeySecurityScheme!;
                    Console.WriteLine($"  --api-key <key> --api-key-header {apiKey.Name}");
                }
                else if (scheme.SchemeCase == SecuritySchemeCase.OAuth2)
                {
                    Console.WriteLine($"  a2a-ask auth login <agent-url>");
                }
                else if (scheme.SchemeCase == SecuritySchemeCase.OpenIdConnect)
                {
                    var oidc = scheme.OpenIdConnectSecurityScheme!;
                    Console.WriteLine($"  --auth-token <token>  (OIDC: {oidc.OpenIdConnectUrl})");
                }
            }
        }
    }

    private void WriteResponseText(SendMessageResponse response, bool verbose)
    {
        if (response.PayloadCase == SendMessageResponseCase.Message)
        {
            var msg = response.Message!;
            Console.WriteLine("[RESPONSE] Direct message from agent:");
            WriteMessageParts(msg);
        }
        else if (response.PayloadCase == SendMessageResponseCase.Task)
        {
            WriteTaskText(response.Task!, verbose);
        }
    }

    private void WriteTaskText(AgentTask task, bool verbose)
    {
        var stateIcon = GetStateIcon(task.Status.State);
        Console.WriteLine($"{stateIcon} Task: {task.Id}");
        Console.WriteLine($"  Context: {task.ContextId}");
        Console.WriteLine($"  State: {task.Status.State}");

        if (task.Status.Message != null)
        {
            Console.WriteLine($"  Status Message:");
            WriteMessageParts(task.Status.Message, "    ");
        }

        if (task.Artifacts != null && task.Artifacts.Count > 0)
        {
            Console.WriteLine($"  Artifacts ({task.Artifacts.Count}):");
            foreach (var artifact in task.Artifacts)
            {
                Console.WriteLine($"    [{artifact.ArtifactId}] {artifact.Name ?? "(unnamed)"}");
                if (artifact.Description != null)
                    Console.WriteLine($"      {artifact.Description}");
                WritePartsList(artifact.Parts, "      ");
            }
        }

        if (verbose && task.History != null && task.History.Count > 0)
        {
            Console.WriteLine($"  History ({task.History.Count} messages):");
            foreach (var msg in task.History)
            {
                Console.WriteLine($"    [{msg.Role}] {msg.MessageId}");
                WriteMessageParts(msg, "      ");
            }
        }
    }

    private static void WriteMessageParts(Message message, string indent = "  ")
    {
        WritePartsList(message.Parts, indent);
    }

    private static void WritePartsList(IList<Part> parts, string indent = "  ")
    {
        foreach (var part in parts)
        {
            if (part.ContentCase == PartContentCase.Text)
            {
                Console.WriteLine($"{indent}{part.Text}");
            }
            else if (part.ContentCase == PartContentCase.Url)
            {
                Console.WriteLine($"{indent}Attachment: {part.Url} ({part.MediaType ?? "unknown type"})");
            }
            else if (part.ContentCase == PartContentCase.Raw)
            {
                Console.WriteLine($"{indent}Binary data ({part.Raw!.Length} bytes, {part.MediaType ?? "unknown type"})");
            }
            else if (part.ContentCase == PartContentCase.Data)
            {
                Console.WriteLine($"{indent}Data: {JsonSerializer.Serialize(part.Data)}");
            }
        }
    }

    private static string GetStateIcon(TaskState state) => state switch
    {
        TaskState.Completed => "Done",
        TaskState.Failed => "Failed",
        TaskState.Working => "Working",
        TaskState.Submitted => "Submitted",
        TaskState.InputRequired => "InputRequired",
        TaskState.AuthRequired => "AuthRequired",
        TaskState.Canceled => "Canceled",
        TaskState.Rejected => "Rejected",
        _ => "Unknown"
    };

    private static string GetSchemeDescription(SecurityScheme scheme)
    {
        if (scheme.SchemeCase == SecuritySchemeCase.HttpAuth)
        {
            var http = scheme.HttpAuthSecurityScheme!;
            return $"HTTP {http.Scheme ?? "Bearer"}" + (http.BearerFormat != null ? $" ({http.BearerFormat})" : "");
        }
        else if (scheme.SchemeCase == SecuritySchemeCase.ApiKey)
        {
            var apiKey = scheme.ApiKeySecurityScheme!;
            return $"API Key (in {apiKey.Location}: {apiKey.Name})";
        }
        else if (scheme.SchemeCase == SecuritySchemeCase.OAuth2)
            return "OAuth 2.0";
        else if (scheme.SchemeCase == SecuritySchemeCase.OpenIdConnect)
        {
            var oidc = scheme.OpenIdConnectSecurityScheme!;
            return $"OpenID Connect ({oidc.OpenIdConnectUrl})";
        }
        else if (scheme.SchemeCase == SecuritySchemeCase.Mtls)
            return "Mutual TLS";
        return "Unknown";
    }

    private static string FormatList(IList<string>? items) =>
        items != null && items.Count > 0 ? string.Join(", ", items) : "(none)";

    #endregion
}
