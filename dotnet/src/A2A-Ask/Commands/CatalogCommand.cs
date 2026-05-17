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
        command.AddCommand(CreateAddCommand());
        command.AddCommand(CreateRemoveCommand());
        return command;
    }

    private static Command CreateListCommand()
    {
        var targetArgument = new Argument<string?>("target", () => null, "Catalog alias, URL, or agent@catalog reference")
        {
            Arity = ArgumentArity.ZeroOrOne
        };
        var filterOption = new Option<string?>("--filter", "Filter catalog agents by text");

        var command = new Command("list", "List A2A agents available in a catalog, or show registered catalog aliases")
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
                var formatter = new ConsoleFormatter(output, pretty);
                var registry = new CatalogRegistry();

                if (string.IsNullOrWhiteSpace(target))
                {
                    WriteRegisteredCatalogs(formatter, output, ApplyCatalogFilter(registry.LoadAliases(), filter));
                    return;
                }

                using var httpClient = new HttpClient();
                var resolver = new CatalogInputResolver(httpClient);
                var agents = await resolver.ResolveAgentsAsync(ResolveCatalogReference(target, registry), context.GetCancellationToken());
                formatter.WriteCatalogAgents(ApplyAgentFilter(agents, filter));
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
        var targetArgument = new Argument<string>("target", "Agent URL, catalog alias, or agent@catalog reference");

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
                var registry = new CatalogRegistry();
                var parseResult = TargetParser.Parse(target);
                var agent = parseResult switch
                {
                    CatalogTarget catalogTarget when !string.IsNullOrWhiteSpace(catalogTarget.CatalogAlias)
                        => await resolver.ResolveAgentAsync(ResolveRegisteredCatalogReference(catalogTarget.CatalogAlias!, registry), catalogTarget.AgentName, context.GetCancellationToken()),
                    CatalogTarget catalogTarget
                        => await SearchRegisteredCatalogsForAgentAsync(catalogTarget.AgentName, registry, resolver, context.GetCancellationToken()),
                    CatalogBrowse browse
                        => await ResolveSingleAgentAsync(resolver, ResolveRegisteredCatalogReference(browse.CatalogAlias, registry), browse.CatalogAlias, context.GetCancellationToken()),
                    UnqualifiedName unqualified when registry.TryGetUrl(unqualified.Name, out var catalogUrl)
                        => await ResolveSingleAgentAsync(resolver, catalogUrl, unqualified.Name, context.GetCancellationToken()),
                    UnqualifiedName unqualified
                        => await SearchRegisteredCatalogsForAgentAsync(unqualified.Name, registry, resolver, context.GetCancellationToken()),
                    DirectUrl directUrl
                        => await ResolveSingleAgentAsync(resolver, directUrl.Url, directUrl.Url, context.GetCancellationToken()),
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

    private static Command CreateAddCommand()
    {
        var aliasArgument = new Argument<string>("alias", "Catalog alias to register");
        var urlArgument = new Argument<string>("url", "Catalog URL to register");
        var noValidateOption = new Option<bool>("--no-validate", "Skip catalog URL validation");

        var command = new Command("add", "Register a catalog alias")
        {
            aliasArgument,
            urlArgument,
            noValidateOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var alias = context.ParseResult.GetValueForArgument(aliasArgument);
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var noValidate = context.ParseResult.GetValueForOption(noValidateOption);
            var verbose = context.ParseResult.GetValueForOption(
                context.ParseResult.RootCommandResult.Command.Options
                    .OfType<Option<bool>>().First(o => o.Name == "verbose"));

            try
            {
                if (!noValidate)
                {
                    using var httpClient = new HttpClient();
                    var resolver = new CatalogInputResolver(httpClient);
                    await resolver.ResolveAgentsAsync(url, context.GetCancellationToken());
                }

                var registry = new CatalogRegistry();
                registry.AddAlias(alias, url);
                Console.WriteLine($"Registered catalog '{alias}' → {url}");
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateRemoveCommand()
    {
        var aliasArgument = new Argument<string>("alias", "Catalog alias to remove");

        var command = new Command("remove", "Remove a registered catalog alias")
        {
            aliasArgument
        };

        command.SetHandler((InvocationContext context) =>
        {
            var alias = context.ParseResult.GetValueForArgument(aliasArgument);
            var verbose = context.ParseResult.GetValueForOption(
                context.ParseResult.RootCommandResult.Command.Options
                    .OfType<Option<bool>>().First(o => o.Name == "verbose"));

            try
            {
                var registry = new CatalogRegistry();
                if (!registry.RemoveAlias(alias))
                {
                    throw new InvalidOperationException($"Catalog alias '{alias}' is not registered.");
                }

                Console.WriteLine($"Removed catalog '{alias}'.");
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static IReadOnlyDictionary<string, string> ApplyCatalogFilter(
        IReadOnlyDictionary<string, string> catalogs,
        string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return catalogs;
        }

        return catalogs
            .Where(catalog => catalog.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || catalog.Value.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(catalog => catalog.Key, catalog => catalog.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ResolvedCatalogAgent> ApplyAgentFilter(IReadOnlyList<ResolvedCatalogAgent> agents, string? filter)
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
        string resolvedCatalogReference,
        string displayCatalogReference,
        CancellationToken cancellationToken)
    {
        var agents = await resolver.ResolveAgentsAsync(resolvedCatalogReference, cancellationToken);
        return agents.Count switch
        {
            0 => throw new InvalidOperationException($"No A2A agents were found in catalog '{displayCatalogReference}'."),
            1 => agents[0],
            _ => throw new InvalidOperationException(
                $"Catalog '{displayCatalogReference}' contains multiple A2A agents. Use <agent>@{displayCatalogReference} to select one explicitly.")
        };
    }

    private static async Task<ResolvedCatalogAgent> SearchRegisteredCatalogsForAgentAsync(
        string agentName,
        CatalogRegistry registry,
        CatalogInputResolver resolver,
        CancellationToken cancellationToken)
    {
        var aliases = registry.LoadAliases();
        if (aliases.Count == 0)
        {
            throw new InvalidOperationException(
                $"No registered catalogs are available to search for '{agentName}'. Use `a2a-ask catalog add <alias> <url>`.");
        }

        var lookups = await Task.WhenAll(
            aliases.Select(async alias =>
            {
                try
                {
                    var agents = await resolver.ResolveAgentsAsync(alias.Value, cancellationToken);
                    return new CatalogLookup(alias.Key, agents, null);
                }
                catch (Exception ex)
                {
                    return new CatalogLookup(alias.Key, null, ex);
                }
            }));

        var catalogAgents = lookups
            .Where(lookup => lookup.Agents != null)
            .SelectMany(lookup => lookup.Agents!.Select(agent => new QualifiedCatalogMatch(lookup.Alias, agent)))
            .ToList();
        var matches = FindMatchingCatalogAgents(catalogAgents, agentName);

        return matches.Count switch
        {
            1 => matches[0].Agent,
            > 1 => throw new InvalidOperationException(
                $"Multiple registered catalogs matched '{agentName}': {string.Join(", ", matches.Select(match => $"{EncodeTargetComponent(match.Agent.EntryId)}@{EncodeTargetComponent(match.Alias)}"))}."),
            _ when lookups.Any(lookup => lookup.Error != null) => throw new InvalidOperationException(
                $"No A2A agent matching '{agentName}' was found. Some catalogs could not be reached: {string.Join(", ", lookups.Where(lookup => lookup.Error != null).Select(lookup => lookup.Alias).OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase))}."),
            _ => throw new InvalidOperationException(
                $"No A2A agent matching '{agentName}' was found in registered catalogs.")
        };
    }

    private static void WriteRegisteredCatalogs(
        ConsoleFormatter formatter,
        string output,
        IReadOnlyDictionary<string, string> catalogs)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        var orderedCatalogs = catalogs
            .OrderBy(catalog => catalog.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var aliases = orderedCatalogs
            .Select(catalog => new RegisteredCatalog(catalog.Key, catalog.Value))
            .ToList();

        if (!string.Equals(output, "text", StringComparison.OrdinalIgnoreCase))
        {
            formatter.WriteJson(aliases);
            return;
        }

        if (orderedCatalogs.Count == 0)
        {
            Console.WriteLine("No registered catalogs.");
            return;
        }

        var aliasWidth = orderedCatalogs.Max(catalog => catalog.Key.Length);
        Console.WriteLine("Registered catalogs:");
        foreach (var (alias, url) in orderedCatalogs)
        {
            Console.WriteLine($"  {alias.PadRight(aliasWidth)}  → {url}");
        }
    }

    private static IReadOnlyList<QualifiedCatalogMatch> FindMatchingCatalogAgents(
        IReadOnlyList<QualifiedCatalogMatch> agents,
        string agentName)
    {
        var exactIdentifierMatches = agents
            .Where(agent => string.Equals(agent.Agent.EntryId, agentName, StringComparison.Ordinal))
            .ToList();
        if (exactIdentifierMatches.Count > 0)
        {
            return exactIdentifierMatches;
        }

        var exactDisplayNameMatches = agents
            .Where(agent => string.Equals(agent.Agent.DisplayName, agentName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exactDisplayNameMatches.Count > 0)
        {
            return exactDisplayNameMatches;
        }

        var exactTagMatches = agents
            .Where(agent => agent.Agent.Tags.Any(tag => string.Equals(tag, agentName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (exactTagMatches.Count > 0)
        {
            return exactTagMatches;
        }

        return agents
            .Where(agent =>
                agent.Agent.EntryId.Contains(agentName, StringComparison.OrdinalIgnoreCase)
                || agent.Agent.DisplayName.Contains(agentName, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(agent.Agent.Description)
                    && agent.Agent.Description.Contains(agentName, StringComparison.OrdinalIgnoreCase))
                || agent.Agent.Tags.Any(tag => tag.Contains(agentName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static string ResolveCatalogReference(string target, CatalogRegistry registry) => TargetParser.Parse(target) switch
    {
        CatalogBrowse browse => ResolveRegisteredCatalogReference(browse.CatalogAlias, registry),
        DirectUrl directUrl => directUrl.Url,
        CatalogTarget catalogTarget when !string.IsNullOrWhiteSpace(catalogTarget.CatalogAlias) => ResolveRegisteredCatalogReference(catalogTarget.CatalogAlias!, registry),
        UnqualifiedName unqualified when registry.TryGetUrl(unqualified.Name, out var registeredUrl) => registeredUrl,
        UnqualifiedName => target.Trim(),
        CatalogTarget => throw new InvalidOperationException("Catalog-qualified targets must include a catalog. Use <agent>@<catalog> or a catalog URL."),
        _ => throw new InvalidOperationException("Unsupported catalog target.")
    };

    private static string ResolveRegisteredCatalogReference(string catalogReference, CatalogRegistry registry) =>
        registry.TryGetUrl(catalogReference, out var registeredUrl)
            ? registeredUrl
            : catalogReference;

    private static string EncodeTargetComponent(string value) => value.Replace(' ', '+');

    private sealed record RegisteredCatalog(string Alias, string Url);

    private sealed record CatalogLookup(string Alias, IReadOnlyList<ResolvedCatalogAgent>? Agents, Exception? Error);

    private sealed record QualifiedCatalogMatch(string Alias, ResolvedCatalogAgent Agent);
}
