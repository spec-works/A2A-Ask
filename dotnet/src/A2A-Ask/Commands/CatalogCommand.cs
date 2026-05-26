using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using A2AAsk.Catalog;
using A2AAsk.Output;

namespace A2AAsk.Commands;

/// <summary>
/// Commands for inspecting AI catalogs that contain A2A agents.
/// </summary>
public static class CatalogCommand
{
    private static readonly JsonSerializerOptions s_cardFetchTelemetryOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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
        command.AddCommand(CreateInstallCommand());
        command.AddCommand(CreateUninstallCommand());
        command.AddCommand(CreateInstalledCommand());
        command.AddCommand(CreateSyncCommand());
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
            var globalOptions = context.GetGlobalOptions();

            try
            {
                var formatter = new ConsoleFormatter(globalOptions.Output, globalOptions.Pretty);
                var registry = new CatalogRegistry();

                if (string.IsNullOrWhiteSpace(target))
                {
                    WriteRegisteredCatalogs(formatter, globalOptions.Output, ApplyCatalogFilter(registry.LoadAliases(), filter));
                    return;
                }

                using var httpClient = new HttpClient();
                var resolver = new CatalogInputResolver(httpClient);
                var agents = await resolver.ResolveAgentsAsync(ResolveCatalogReference(target, registry), context.GetCancellationToken());
                formatter.WriteCatalogAgents(ApplyAgentFilter(agents, filter));
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, globalOptions.Verbose);
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
            var globalOptions = context.GetGlobalOptions();

            try
            {
                using var httpClient = new HttpClient();
                var resolver = new CatalogInputResolver(httpClient);
                var registry = new CatalogRegistry();
                var parseResult = TargetParser.Parse(target);
                var agent = parseResult switch
                {
                    CatalogTarget catalogTarget
                        => await resolver.ResolveAgentAsync(ResolveRegisteredCatalogReference(catalogTarget.CatalogAlias!, registry), catalogTarget.AgentName, context.GetCancellationToken()),
                    UnqualifiedName unqualified when registry.TryGetUrl(unqualified.Name, out var catalogUrl)
                        => await ResolveSingleAgentAsync(resolver, catalogUrl, unqualified.Name, context.GetCancellationToken()),
                    UnqualifiedName unqualified
                        => await SearchRegisteredCatalogsForAgentAsync(unqualified.Name, registry, resolver, context.GetCancellationToken()),
                    DirectUrl directUrl
                        => await ResolveSingleAgentAsync(resolver, directUrl.Url, directUrl.Url, context.GetCancellationToken()),
                    _ => throw new InvalidOperationException("Unsupported catalog target.")
                };

                var formatter = new ConsoleFormatter(globalOptions.Output, globalOptions.Pretty);
                formatter.WriteCatalogAgent(agent);
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, globalOptions.Verbose);
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
            var globalOptions = context.GetGlobalOptions();

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
                Console.WriteLine($"Registered catalog '{alias}' -> {url}");
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, globalOptions.Verbose);
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
            var globalOptions = context.GetGlobalOptions();

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
                ConsoleFormatter.WriteError(ex, globalOptions.Verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateInstallCommand()
    {
        var targetArgument = new Argument<string>("target", "Agent@catalog reference or direct catalog URL");
        var nameOption = new Option<string?>("--name", "Override the generated Copilot agent name");
        var overwriteOption = new Option<bool>("--overwrite", "Replace an existing bridge file");
        var dryRunOption = new Option<bool>("--dry-run", "Print the generated bridge without writing it");
        var skipAuthCheckOption = new Option<bool>("--skip-auth-check", "Skip the authentication pre-flight warning");

        var command = new Command("install", "Install a catalog agent as a Copilot CLI custom agent")
        {
            targetArgument,
            nameOption,
            overwriteOption,
            dryRunOption,
            skipAuthCheckOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var target = context.ParseResult.GetValueForArgument(targetArgument);
            var requestedName = context.ParseResult.GetValueForOption(nameOption);
            var overwrite = context.ParseResult.GetValueForOption(overwriteOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var skipAuthCheck = context.ParseResult.GetValueForOption(skipAuthCheckOption);
            var globalOptions = context.GetGlobalOptions();

            try
            {
                using var httpClient = new HttpClient();
                var registry = new CatalogRegistry();
                var resolver = new CatalogInputResolver(httpClient);
                var resolved = await ResolveInstallTargetAsync(target, registry, resolver, context.GetCancellationToken());
                var fetchedCard = await FetchAgentCardAsync(
                    httpClient,
                    resolved.Agent.AgentCardUrl,
                    context.GetCancellationToken(),
                    verbose: globalOptions.Verbose);
                if (fetchedCard.Card is null || fetchedCard.Hash is null)
                {
                    throw new InvalidOperationException("Catalog install did not receive an agent card payload.");
                }

                var installName = ResolveInstallName(requestedName, fetchedCard.Card);
                EnsureInstallNameIsValid(installName);

                var installDirectory = GetCopilotAgentsDirectory();
                var filePath = Path.Combine(installDirectory, $"{installName}.md");
                if (File.Exists(filePath) && !overwrite)
                {
                    Console.WriteLine($"Skipped '{installName}' because '{filePath}' already exists. Use --overwrite to replace it.");
                    return;
                }

                if (!skipAuthCheck && fetchedCard.Card.RequiresAuthentication)
                {
                    Console.Error.WriteLine($"Warning: This agent card declares security schemes. You may need to run `a2a-ask auth login \"{resolved.CatalogTarget}\"` before using '{installName}'.");
                }

                var installedAt = DateTimeOffset.UtcNow.ToString("O");
                var generator = new BridgeGenerator();
                var markdown = generator.GenerateMarkdown(new BridgeTemplateModel(
                    installName,
                    resolved.CatalogTarget,
                    string.IsNullOrWhiteSpace(resolved.Agent.DisplayName) ? fetchedCard.Card.Name : resolved.Agent.DisplayName,
                    fetchedCard.Card,
                    new BridgeRemoteAgentMetadata(
                        resolved.Agent.CatalogUrl,
                        resolved.Agent.EntryId,
                        resolved.Agent.AgentCardUrl,
                        fetchedCard.ETag,
                        fetchedCard.Hash,
                        installedAt)));

                if (dryRun)
                {
                    WriteInstallDryRun(globalOptions, installName, filePath, markdown, fetchedCard.Card.RequiresAuthentication);
                    return;
                }

                Directory.CreateDirectory(installDirectory);
                await File.WriteAllTextAsync(filePath, markdown, context.GetCancellationToken());
                Console.WriteLine($"Installed '{installName}' to {filePath}");
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, globalOptions.Verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateUninstallCommand()
    {
        var nameArgument = new Argument<string>("name", "Installed Copilot bridge agent name");

        var command = new Command("uninstall", "Remove an installed A2A Copilot bridge")
        {
            nameArgument
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var name = context.ParseResult.GetValueForArgument(nameArgument);
            var globalOptions = context.GetGlobalOptions();

            try
            {
                var filePath = Path.Combine(GetCopilotAgentsDirectory(), $"{name}.md");
                if (!File.Exists(filePath))
                {
                    throw new InvalidOperationException($"No installed bridge named '{name}' was found at '{filePath}'.");
                }

                var markdown = await File.ReadAllTextAsync(filePath, context.GetCancellationToken());
                if (!FrontmatterReader.TryRead(markdown, out _))
                {
                    throw new InvalidOperationException($"Refusing to delete '{filePath}' because it does not contain A2A remote-agent frontmatter.");
                }

                File.Delete(filePath);
                Console.WriteLine($"Uninstalled '{name}' from {filePath}");
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, globalOptions.Verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateInstalledCommand()
    {
        var command = new Command("installed", "List installed A2A Copilot bridge agents");

        command.SetHandler(async (InvocationContext context) =>
        {
            var globalOptions = context.GetGlobalOptions();

            try
            {
                var installed = await GetInstalledBridgesAsync(context.GetCancellationToken());
                var summaries = installed
                    .OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(agent => new InstalledBridgeSummary(
                        agent.Name,
                        agent.Frontmatter.Catalog,
                        agent.Frontmatter.EntryId,
                        agent.Frontmatter.CardUrl,
                        agent.Frontmatter.InstalledAt,
                        agent.FilePath))
                    .ToList();

                if (!string.Equals(globalOptions.Output, "text", StringComparison.OrdinalIgnoreCase))
                {
                    var formatter = new ConsoleFormatter(globalOptions.Output, globalOptions.Pretty);
                    formatter.WriteJson(summaries);
                    return;
                }

                WriteInstalledBridgesText(summaries);
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, globalOptions.Verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateSyncCommand()
    {
        var nameArgument = new Argument<string?>("name", () => null, "Installed bridge name to sync")
        {
            Arity = ArgumentArity.ZeroOrOne
        };

        var command = new Command("sync", "Refresh installed A2A Copilot bridges from their agent cards")
        {
            nameArgument
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var requestedName = context.ParseResult.GetValueForArgument(nameArgument);
            var globalOptions = context.GetGlobalOptions();

            try
            {
                var installed = await GetInstalledBridgesAsync(context.GetCancellationToken());
                var selected = string.IsNullOrWhiteSpace(requestedName)
                    ? installed
                    : installed
                        .Where(agent => string.Equals(agent.Name, requestedName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                if (!string.IsNullOrWhiteSpace(requestedName) && selected.Count == 0)
                {
                    throw new InvalidOperationException($"No installed bridge named '{requestedName}' was found.");
                }

                using var httpClient = new HttpClient();
                var generator = new BridgeGenerator();
                var results = new List<SyncResult>();

                foreach (var bridge in selected.OrderBy(agent => agent.Name, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var markdown = await File.ReadAllTextAsync(bridge.FilePath, context.GetCancellationToken());
                        var fetchedCard = await FetchAgentCardAsync(
                            httpClient,
                            bridge.Frontmatter.CardUrl,
                            context.GetCancellationToken(),
                            bridge.Frontmatter.CardEtag,
                            globalOptions.Verbose);
                        if (fetchedCard.Cached)
                        {
                            results.Add(new SyncResult(bridge.Name, bridge.FilePath, false, null));
                            continue;
                        }

                        if (fetchedCard.Card is null || fetchedCard.Hash is null)
                        {
                            throw new InvalidOperationException("Catalog sync did not receive an agent card payload.");
                        }

                        var needsUpdate = !string.Equals(bridge.Frontmatter.CardEtag, fetchedCard.ETag, StringComparison.Ordinal)
                            || !string.Equals(bridge.Frontmatter.CardHash, fetchedCard.Hash, StringComparison.OrdinalIgnoreCase)
                            || string.IsNullOrWhiteSpace(bridge.Frontmatter.CardHash)
                            || string.IsNullOrWhiteSpace(bridge.Frontmatter.InstalledAt);

                        if (!needsUpdate)
                        {
                            results.Add(new SyncResult(bridge.Name, bridge.FilePath, false, null));
                            continue;
                        }

                        var catalogTarget = TryExtractCatalogTarget(markdown)
                            ?? $"{EncodeTargetComponent(bridge.Frontmatter.EntryId)}@{bridge.Frontmatter.Catalog}";
                        var displayName = TryExtractDisplayName(markdown)
                            ?? fetchedCard.Card.Name;
                        var updatedMetadata = new BridgeRemoteAgentMetadata(
                            bridge.Frontmatter.Catalog,
                            bridge.Frontmatter.EntryId,
                            bridge.Frontmatter.CardUrl,
                            fetchedCard.ETag,
                            fetchedCard.Hash,
                            DateTimeOffset.UtcNow.ToString("O"));
                        var updatedSection = generator.GenerateGeneratedSection(new BridgeTemplateModel(
                            bridge.Name,
                            catalogTarget,
                            displayName,
                            fetchedCard.Card,
                            updatedMetadata));

                        var updatedMarkdown = BridgeGenerator.ReplaceGeneratedSection(markdown, updatedSection);
                        updatedMarkdown = ReplaceRemoteAgentBlock(updatedMarkdown, bridge.Frontmatter, updatedMetadata);
                        await File.WriteAllTextAsync(bridge.FilePath, updatedMarkdown, context.GetCancellationToken());
                        results.Add(new SyncResult(bridge.Name, bridge.FilePath, true, null));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new SyncResult(bridge.Name, bridge.FilePath, false, ex.Message));
                    }
                }

                WriteSyncResults(globalOptions, results);
                if (results.Any(result => result.Error is not null))
                {
                    context.ExitCode = 1;
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, globalOptions.Verbose);
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
            Console.WriteLine($"  {alias.PadRight(aliasWidth)}  -> {url}");
        }
    }

    private static void WriteInstallDryRun(
        GlobalOptionValues globalOptions,
        string name,
        string filePath,
        string markdown,
        bool requiresAuthentication)
    {
        if (!string.Equals(globalOptions.Output, "text", StringComparison.OrdinalIgnoreCase))
        {
            var formatter = new ConsoleFormatter(globalOptions.Output, globalOptions.Pretty);
            formatter.WriteJson(new InstallDryRunResult(name, filePath, requiresAuthentication, markdown));
            return;
        }

        Console.WriteLine($"Dry run: would write '{name}' to {filePath}");
        if (requiresAuthentication)
        {
            Console.WriteLine("Warning: the agent card declares security schemes.");
        }

        Console.WriteLine();
        Console.WriteLine(markdown);
    }

    private static async Task<InstallResolution> ResolveInstallTargetAsync(
        string target,
        CatalogRegistry registry,
        CatalogInputResolver resolver,
        CancellationToken cancellationToken)
    {
        var parseResult = TargetParser.Parse(target);
        switch (parseResult)
        {
            case CatalogTarget catalogTarget:
            {
                var resolvedCatalogReference = ResolveRegisteredCatalogReference(catalogTarget.CatalogAlias!, registry);
                var agent = await resolver.ResolveAgentAsync(resolvedCatalogReference, catalogTarget.AgentName, cancellationToken);
                return new InstallResolution(
                    agent,
                    $"{EncodeTargetComponent(agent.EntryId)}@{EncodeTargetComponent(catalogTarget.CatalogAlias!)}");
            }
            case DirectUrl directUrl:
                return new InstallResolution(
                    await ResolveSingleAgentAsync(resolver, directUrl.Url, directUrl.Url, cancellationToken),
                    directUrl.Url.Trim());
            default:
                throw new InvalidOperationException("Install requires an agent@catalog reference or a direct catalog URL.");
        }
    }

    private static string ResolveInstallName(string? requestedName, BridgeAgentCard card)
    {
        var source = string.IsNullOrWhiteSpace(requestedName) ? card.Name : requestedName;
        return BridgeGenerator.GenerateKebabCaseName(source);
    }

    private static void EnsureInstallNameIsValid(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("A non-empty bridge name is required.");
        }

        if (BridgeGenerator.IsReservedName(name))
        {
            throw new InvalidOperationException($"'{name}' collides with a built-in Copilot agent name. Choose a different bridge name.");
        }
    }

    internal static async Task<FetchedAgentCard> FetchAgentCardAsync(
        HttpClient httpClient,
        string cardUrl,
        CancellationToken cancellationToken,
        string? storedEtag = null,
        bool verbose = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, cardUrl);
        if (!string.IsNullOrWhiteSpace(storedEtag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", storedEtag);
        }

        var stopwatch = Stopwatch.StartNew();
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            stopwatch.Stop();
            WriteCardFetchTelemetry(cardUrl, (int)response.StatusCode, cached: true, stopwatch.ElapsedMilliseconds, verbose);
            return new FetchedAgentCard(null, null, null, Cached: true);
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            stopwatch.Stop();
            WriteCardFetchTelemetry(cardUrl, (int)response.StatusCode, cached: false, stopwatch.ElapsedMilliseconds, verbose);
            response.EnsureSuccessStatusCode();
        }

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        using var document = JsonDocument.Parse(content);
        var etag = response.Headers.ETag?.ToString();
        var hash = BridgeGenerator.ComputeSha256(content);
        var result = new FetchedAgentCard(
            BridgeGenerator.ParseAgentCard(document.RootElement),
            etag,
            hash,
            Cached: false);

        stopwatch.Stop();
        WriteCardFetchTelemetry(cardUrl, (int)response.StatusCode, cached: false, stopwatch.ElapsedMilliseconds, verbose, etag, hash);
        return result;
    }

    private static void WriteCardFetchTelemetry(
        string cardUrl,
        int status,
        bool cached,
        long elapsedMilliseconds,
        bool verbose,
        string? etag = null,
        string? hash = null)
    {
        if (!verbose)
        {
            return;
        }

        Console.Error.WriteLine(JsonSerializer.Serialize(
            new CardFetchTelemetry("card-fetch", cardUrl, status, etag, hash, cached, elapsedMilliseconds),
            s_cardFetchTelemetryOptions));
    }

    private static async Task<List<InstalledBridgeAgent>> GetInstalledBridgesAsync(CancellationToken cancellationToken)
    {
        var directory = GetCopilotAgentsDirectory();
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var installed = new List<InstalledBridgeAgent>();
        foreach (var filePath in Directory.EnumerateFiles(directory, "*.md", SearchOption.TopDirectoryOnly))
        {
            var markdown = await File.ReadAllTextAsync(filePath, cancellationToken);
            if (!FrontmatterReader.TryRead(markdown, out var frontmatter) || frontmatter is null)
            {
                continue;
            }

            installed.Add(new InstalledBridgeAgent(
                frontmatter.Name ?? Path.GetFileNameWithoutExtension(filePath),
                filePath,
                frontmatter));
        }

        return installed;
    }

    private static void WriteInstalledBridgesText(IReadOnlyList<InstalledBridgeSummary> installed)
    {
        if (installed.Count == 0)
        {
            Console.WriteLine("No installed A2A bridge agents.");
            return;
        }

        foreach (var bridge in installed)
        {
            Console.WriteLine($"- {bridge.Name}");
            Console.WriteLine($"  Catalog: {bridge.Catalog}");
            Console.WriteLine($"  Entry: {bridge.EntryId}");
            Console.WriteLine($"  Card: {bridge.CardUrl}");
            if (!string.IsNullOrWhiteSpace(bridge.InstalledAt))
            {
                Console.WriteLine($"  Installed: {bridge.InstalledAt}");
            }
        }
    }

    private static void WriteSyncResults(GlobalOptionValues globalOptions, IReadOnlyList<SyncResult> results)
    {
        if (!string.Equals(globalOptions.Output, "text", StringComparison.OrdinalIgnoreCase))
        {
            var formatter = new ConsoleFormatter(globalOptions.Output, globalOptions.Pretty);
            formatter.WriteJson(results);
            return;
        }

        if (results.Count == 0)
        {
            Console.WriteLine("No installed A2A bridge agents to sync.");
            return;
        }

        foreach (var result in results)
        {
            if (result.Error is not null)
            {
                Console.WriteLine($"- {result.Name}: failed ({result.Error})");
            }
            else if (result.Updated)
            {
                Console.WriteLine($"- {result.Name}: updated");
            }
            else
            {
                Console.WriteLine($"- {result.Name}: unchanged");
            }
        }
    }

    private static string ReplaceRemoteAgentBlock(
        string markdown,
        RemoteAgentFrontmatter frontmatter,
        BridgeRemoteAgentMetadata metadata)
    {
        var replacementLines = BridgeGenerator.GenerateRemoteAgentBlockLines(metadata);
        return ReplaceLineRange(markdown, frontmatter.RemoteAgentStartLineIndex, frontmatter.RemoteAgentEndLineIndex, replacementLines);
    }

    private static string ReplaceLineRange(string markdown, int startLine, int endLine, IReadOnlyList<string> replacementLines)
    {
        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);
        var hadTrailingNewline = normalized.EndsWith('\n');
        var lines = normalized.Split('\n').ToList();
        if (hadTrailingNewline && lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        lines.RemoveRange(startLine, endLine - startLine + 1);
        lines.InsertRange(startLine, replacementLines);

        var rebuilt = string.Join(Environment.NewLine, lines);
        return hadTrailingNewline ? rebuilt + Environment.NewLine : rebuilt;
    }

    private static string? TryExtractCatalogTarget(string markdown)
    {
        const string prefix = "a2a-ask send \"";
        var index = markdown.IndexOf(prefix, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        var start = index + prefix.Length;
        var end = markdown.IndexOf('"', start);
        return end > start ? markdown[start..end] : null;
    }

    private static string? TryExtractDisplayName(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("# ", StringComparison.Ordinal) && line.EndsWith(" (A2A bridge)", StringComparison.Ordinal))
            {
                return line[2..^13];
            }
        }

        return null;
    }

    private static IReadOnlyList<QualifiedCatalogMatch> FindMatchingCatalogAgents(
        IReadOnlyList<QualifiedCatalogMatch> agents,
        string agentName)
    {
        var matches = CatalogInputResolver.FindMatchingAgents(
            agents.Select(agent => agent.Agent).ToList(),
            agentName);
        return agents
            .Where(agent => matches.Contains(agent.Agent))
            .ToList();
    }

    private static string ResolveCatalogReference(string target, CatalogRegistry registry) => TargetParser.Parse(target) switch
    {
        DirectUrl directUrl => directUrl.Url,
        CatalogTarget catalogTarget => ResolveRegisteredCatalogReference(catalogTarget.CatalogAlias!, registry),
        UnqualifiedName unqualified when registry.TryGetUrl(unqualified.Name, out var registeredUrl) => registeredUrl,
        UnqualifiedName => target.Trim(),
        _ => throw new InvalidOperationException("Unsupported catalog target.")
    };

    private static string ResolveRegisteredCatalogReference(string catalogReference, CatalogRegistry registry) =>
        registry.TryGetUrl(catalogReference, out var registeredUrl)
            ? registeredUrl
            : catalogReference;

    private static string GetCopilotAgentsDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilot",
        "agents");

    private static string EncodeTargetComponent(string value) => value.Replace(' ', '+');

    private sealed record RegisteredCatalog(string Alias, string Url);

    private sealed record CatalogLookup(string Alias, IReadOnlyList<ResolvedCatalogAgent>? Agents, Exception? Error);

    private sealed record QualifiedCatalogMatch(string Alias, ResolvedCatalogAgent Agent);

    private sealed record InstallResolution(ResolvedCatalogAgent Agent, string CatalogTarget);

    internal sealed record FetchedAgentCard(BridgeAgentCard? Card, string? ETag, string? Hash, bool Cached);

    private sealed record CardFetchTelemetry(string op, string url, int status, string? etag, string? hash, bool cached, long elapsed_ms);

    private sealed record InstalledBridgeAgent(string Name, string FilePath, RemoteAgentFrontmatter Frontmatter);

    private sealed record InstalledBridgeSummary(
        string Name,
        string Catalog,
        string EntryId,
        string CardUrl,
        string? InstalledAt,
        string FilePath);

    private sealed record InstallDryRunResult(string Name, string FilePath, bool RequiresAuthentication, string Content);

    private sealed record SyncResult(string Name, string FilePath, bool Updated, string? Error);
}
