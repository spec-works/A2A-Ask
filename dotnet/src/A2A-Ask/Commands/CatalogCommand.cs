using System.CommandLine;
using System.CommandLine.Invocation;
using A2AAsk.Catalog;
using A2AAsk.Output;

namespace A2AAsk.Commands;

/// <summary>
/// Commands for inspecting AI catalogs that contain A2A agents.
/// </summary>
public static class CatalogCommand
{
    /// <summary>
    /// Creates the catalog command group.
    /// </summary>
    /// <returns>The configured command.</returns>
    public static Command Create()
    {
        var command = new Command("catalog", "Inspect AI catalogs that contain A2A agents");
        command.AddCommand(CreateListCommand());
        command.AddCommand(CreateShowCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var targetArgument = new Argument<string>("target", "Catalog URL, host, or @@catalog reference");
        var filterOption = new Option<string?>("--filter", "Filter catalog agents by text");

        var command = new Command("list", "List A2A agents available in a catalog")
        {
            targetArgument,
            filterOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var target = context.ParseResult.GetValueForArgument(targetArgument);
            var filter = context.ParseResult.GetValueForOption(filterOption);
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
                using var httpClient = new HttpClient();
                var resolver = new CatalogInputResolver(httpClient);
                var agents = await resolver.ResolveAgentsAsync(ResolveCatalogReference(target), context.GetCancellationToken());
                var filteredAgents = ApplyFilter(agents, filter);

                var formatter = new ConsoleFormatter(output, pretty);
                formatter.WriteCatalogAgents(filteredAgents);
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateShowCommand()
    {
        var targetArgument = new Argument<string>("target", "Catalog URL, host, @@catalog reference, or @agent@catalog target");

        var command = new Command("show", "Show one resolved A2A agent from a catalog")
        {
            targetArgument
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var target = context.ParseResult.GetValueForArgument(targetArgument);
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
                using var httpClient = new HttpClient();
                var resolver = new CatalogInputResolver(httpClient);
                var parseResult = TargetParser.Parse(target);
                var agent = parseResult switch
                {
                    CatalogTarget catalogTarget when !string.IsNullOrWhiteSpace(catalogTarget.CatalogAlias)
                        => await resolver.ResolveAgentAsync(catalogTarget.CatalogAlias!, catalogTarget.AgentName, context.GetCancellationToken()),
                    CatalogTarget
                        => throw new InvalidOperationException("Phase 1 requires a catalog host or URL. Use @agent@catalog or a catalog URL."),
                    CatalogBrowse browse
                        => await ResolveSingleAgentAsync(resolver, browse.CatalogAlias, context.GetCancellationToken()),
                    DirectUrl directUrl
                        => await ResolveSingleAgentAsync(resolver, directUrl.Url, context.GetCancellationToken()),
                    _ => throw new InvalidOperationException("Unsupported catalog target.")
                };

                var formatter = new ConsoleFormatter(output, pretty);
                formatter.WriteCatalogAgent(agent);
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static IReadOnlyList<ResolvedCatalogAgent> ApplyFilter(IReadOnlyList<ResolvedCatalogAgent> agents, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return agents;
        }

        return agents
            .Where(agent => agent.EntryId.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || agent.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(agent.Description)
                    && agent.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
                || agent.Tags.Any(tag => tag.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static async Task<ResolvedCatalogAgent> ResolveSingleAgentAsync(
        CatalogInputResolver resolver,
        string catalogReference,
        CancellationToken cancellationToken)
    {
        var agents = await resolver.ResolveAgentsAsync(ResolveCatalogReference(catalogReference), cancellationToken);
        return agents.Count switch
        {
            0 => throw new InvalidOperationException($"No A2A agents were found in catalog '{catalogReference}'."),
            1 => agents[0],
            _ => throw new InvalidOperationException(
                $"Catalog '{catalogReference}' contains multiple A2A agents. Use @<agent>@{catalogReference} to select one explicitly.")
        };
    }

    private static string ResolveCatalogReference(string target) => TargetParser.Parse(target) switch
    {
        CatalogBrowse browse => browse.CatalogAlias,
        DirectUrl directUrl => directUrl.Url,
        CatalogTarget catalogTarget when !string.IsNullOrWhiteSpace(catalogTarget.CatalogAlias) => catalogTarget.CatalogAlias!,
        CatalogTarget => throw new InvalidOperationException("Phase 1 requires a catalog host or URL. Use @agent@catalog or a catalog URL."),
        _ => throw new InvalidOperationException("Unsupported catalog target.")
    };
}
