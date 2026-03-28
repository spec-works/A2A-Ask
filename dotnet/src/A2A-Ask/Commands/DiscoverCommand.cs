using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using A2A;
using A2AAsk.Auth;
using A2AAsk.Output;

namespace A2AAsk.Commands;

public static class DiscoverCommand
{
    public static Command Create()
    {
        var urlArgument = new Argument<string>(
            name: "url",
            description: "Base URL of the agent (or direct agent card URL)");

        var wellKnownOption = new Option<bool>(
            name: "--well-known",
            description: "Append /.well-known/agent-card.json to the URL",
            getDefaultValue: () => true);

        var extendedOption = new Option<bool>(
            name: "--extended",
            description: "Fetch the extended (authenticated) agent card",
            getDefaultValue: () => false);

        var authTokenOption = CommonOptions.AuthToken();
        var authHeaderOption = CommonOptions.AuthHeader();

        var command = new Command("discover", "Fetch and display an A2A agent card")
        {
            urlArgument,
            wellKnownOption,
            extendedOption,
            authTokenOption,
            authHeaderOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var wellKnown = context.ParseResult.GetValueForOption(wellKnownOption);
            var extended = context.ParseResult.GetValueForOption(extendedOption);
            var authToken = context.ParseResult.GetValueForOption(authTokenOption);
            var authHeader = context.ParseResult.GetValueForOption(authHeaderOption);
            var output = context.ParseResult.GetValueForOption(
                context.ParseResult.RootCommandResult.Command.Options
                    .OfType<Option<string>>().First(o => o.Name == "output"))!;
            var pretty = context.ParseResult.GetValueForOption(
                context.ParseResult.RootCommandResult.Command.Options
                    .OfType<Option<bool>>().First(o => o.Name == "pretty"));
            var verbose = context.ParseResult.GetValueForOption(
                context.ParseResult.RootCommandResult.Command.Options
                    .OfType<Option<bool>>().First(o => o.Name == "verbose"));

            try
            {
                var httpClient = await AuthConfigurator.CreateHttpClientWithStoredTokenAsync(
                    url,
                    authToken: authToken,
                    authHeader: authHeader);

                var useWellKnown = wellKnown && !url.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

                // Try SDK-based agent card resolution first
                try
                {
                    A2ACardResolver resolver;
                    if (useWellKnown)
                    {
                        var baseUri = new Uri(url.TrimEnd('/'));
                        resolver = new A2ACardResolver(baseUri, httpClient, "/.well-known/agent-card.json");
                    }
                    else
                    {
                        var fullUri = new Uri(url);
                        var baseUri = new Uri($"{fullUri.Scheme}://{fullUri.Authority}");
                        resolver = new A2ACardResolver(baseUri, httpClient, fullUri.PathAndQuery);
                    }

                    if (extended)
                        Console.Error.WriteLine("Warning: Extended agent card is not supported in this SDK version. Showing public card.");

                    var card = await resolver.GetAgentCardAsync();
                    var formatter = new ConsoleFormatter(output, pretty);
                    formatter.WriteAgentCard(card, verbose);
                    return;
                }
                catch (A2AException ex)
                {
                    if (verbose)
                        Console.Error.WriteLine($"SDK parse failed ({ex.Message}). Trying raw JSON fallback...");
                    // Fall through to raw JSON fallback
                }

                var cardUrl = useWellKnown
                    ? $"{url.TrimEnd('/')}/.well-known/agent-card.json"
                    : url;

                var response = await httpClient.GetAsync(cardUrl);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                if (output == "json")
                {
                    var opts = new JsonSerializerOptions { WriteIndented = pretty };
                    Console.WriteLine(JsonSerializer.Serialize(doc.RootElement, opts));
                }
                else
                {
                    // Best-effort text rendering from raw JSON
                    var root = doc.RootElement;
                    if (root.TryGetProperty("name", out var name))
                        Console.WriteLine($"Agent: {name.GetString()}");
                    if (root.TryGetProperty("description", out var desc))
                        Console.WriteLine($"Description: {desc.GetString()}");
                    if (root.TryGetProperty("version", out var ver))
                        Console.WriteLine($"Version: {ver.GetString()}");
                    if (root.TryGetProperty("url", out var agentUrl))
                        Console.WriteLine($"URL: {agentUrl.GetString()}");
                    if (root.TryGetProperty("protocolVersion", out var pv))
                        Console.WriteLine($"Protocol Version: {pv.GetString()}");

                    if (root.TryGetProperty("capabilities", out var caps))
                    {
                        Console.WriteLine();
                        Console.WriteLine("Capabilities:");
                        foreach (var prop in caps.EnumerateObject())
                            Console.WriteLine($"  {prop.Name}: {prop.Value}");
                    }

                    if (root.TryGetProperty("skills", out var skills) && skills.ValueKind == JsonValueKind.Array)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Skills:");
                        foreach (var skill in skills.EnumerateArray())
                        {
                            var sid = skill.TryGetProperty("id", out var id) ? id.GetString() : "?";
                            var sname = skill.TryGetProperty("name", out var sn) ? sn.GetString() : "?";
                            var sdesc = skill.TryGetProperty("description", out var sd) ? sd.GetString() : "";
                            Console.WriteLine($"  [{sid}] {sname}");
                            if (!string.IsNullOrEmpty(sdesc))
                                Console.WriteLine($"    {sdesc}");
                        }
                    }

                    Console.Error.WriteLine();
                    Console.Error.WriteLine("⚠ This agent uses an older protocol version. send/stream commands may not work.");
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }
}
