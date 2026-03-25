using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using A2A;
using A2AAsk.Auth;
using A2AAsk.Output;

namespace A2AAsk.Commands;

public static class StreamCommand
{
    public static Command Create()
    {
        var urlArgument = new Argument<string>(
            name: "url",
            description: "Agent endpoint URL");

        var messageOption = new Option<string?>(
            aliases: ["--message", "-m"],
            description: "Message text to send");

        var fileOption = new Option<FileInfo?>(
            aliases: ["--file", "-f"],
            description: "File to include as a message part");

        var dataOption = new Option<string?>(
            aliases: ["--data", "-d"],
            description: "Structured JSON data part");

        var subscribeOption = new Option<bool>(
            name: "--subscribe",
            description: "Subscribe to an existing task's events (requires --task-id)",
            getDefaultValue: () => false);

        var taskIdOption = CommonOptions.TaskId();
        var contextIdOption = CommonOptions.ContextId();
        var acceptOption = new Option<string?>(name: "--accept", description: "Accepted output modes (comma-separated)");
        var historyLengthOption = new Option<int?>(name: "--history-length", description: "Max history messages");
        var authTokenOption = CommonOptions.AuthToken();
        var authHeaderOption = CommonOptions.AuthHeader();
        var apiKeyOption = CommonOptions.ApiKey();
        var apiKeyHeaderOption = CommonOptions.ApiKeyHeader();
        var tenantOption = CommonOptions.Tenant();
        var saveArtifactsOption = CommonOptions.SaveArtifacts();

        var command = new Command("stream", "Send a message with streaming response, or subscribe to task events")
        {
            urlArgument,
            messageOption,
            fileOption,
            dataOption,
            subscribeOption,
            taskIdOption,
            contextIdOption,
            acceptOption,
            historyLengthOption,
            authTokenOption,
            authHeaderOption,
            apiKeyOption,
            apiKeyHeaderOption,
            tenantOption,
            saveArtifactsOption
        };

        command.AddValidator(result =>
        {
            var subscribe = result.GetValueForOption(subscribeOption);
            var taskId = result.GetValueForOption(taskIdOption);
            var message = result.GetValueForOption(messageOption);
            var file = result.GetValueForOption(fileOption);
            var data = result.GetValueForOption(dataOption);

            if (subscribe && string.IsNullOrEmpty(taskId))
            {
                result.ErrorMessage = "--subscribe requires --task-id to specify which task to subscribe to.";
            }
            else if (!subscribe && string.IsNullOrEmpty(message) && file == null && string.IsNullOrEmpty(data))
            {
                result.ErrorMessage = "At least one of --message, --file, or --data is required (unless using --subscribe).";
            }
        });

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var message = context.ParseResult.GetValueForOption(messageOption);
            var file = context.ParseResult.GetValueForOption(fileOption);
            var data = context.ParseResult.GetValueForOption(dataOption);
            var subscribe = context.ParseResult.GetValueForOption(subscribeOption);
            var taskId = context.ParseResult.GetValueForOption(taskIdOption);
            var contextId = context.ParseResult.GetValueForOption(contextIdOption);
            var accept = context.ParseResult.GetValueForOption(acceptOption);
            var historyLength = context.ParseResult.GetValueForOption(historyLengthOption);
            var authToken = context.ParseResult.GetValueForOption(authTokenOption);
            var authHeader = context.ParseResult.GetValueForOption(authHeaderOption);
            var apiKey = context.ParseResult.GetValueForOption(apiKeyOption);
            var apiKeyHeader = context.ParseResult.GetValueForOption(apiKeyHeaderOption);
            var tenant = context.ParseResult.GetValueForOption(tenantOption);
            var saveArtifacts = context.ParseResult.GetValueForOption(saveArtifactsOption);
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
                var httpClient = AuthConfigurator.CreateHttpClient(
                    authToken: authToken,
                    authHeader: authHeader,
                    apiKey: apiKey,
                    apiKeyHeader: apiKeyHeader);

                var formatter = new ConsoleFormatter(output, pretty);
                var renderer = new ProgressRenderer(output);
                var ct = context.GetCancellationToken();

                var client = await CommonOptions.CreateClientAsync(
                    url, httpClient, ct);

                IAsyncEnumerable<StreamResponse> stream;

                if (subscribe)
                {
                    stream = client.SubscribeToTaskAsync(
                        new SubscribeToTaskRequest { Id = taskId! }, ct);
                }
                else
                {
                    // Build message parts
                    var parts = new List<Part>();
                    if (!string.IsNullOrEmpty(message))
                        parts.Add(new Part { Text = message });
                    if (file != null)
                    {
                        var bytes = File.ReadAllBytes(file.FullName);
                        parts.Add(new Part { Raw = bytes, Filename = file.Name });
                    }
                    if (!string.IsNullOrEmpty(data))
                    {
                        var jsonElement = JsonSerializer.SerializeToElement(
                            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(data));
                        parts.Add(new Part { Data = jsonElement });
                    }

                    var msg = new Message
                    {
                        Role = Role.User,
                        MessageId = Guid.NewGuid().ToString(),
                        Parts = parts,
                        TaskId = taskId,
                        ContextId = contextId
                    };
                    var config = new SendMessageConfiguration();
                    if (!string.IsNullOrEmpty(accept))
                        config.AcceptedOutputModes = accept.Split(',', StringSplitOptions.TrimEntries).ToList();
                    if (historyLength.HasValue)
                        config.HistoryLength = historyLength.Value;

                    var request = new SendMessageRequest
                    {
                        Message = msg,
                        Configuration = config
                    };

                    stream = client.SendStreamingMessageAsync(request, ct);
                }

                var allArtifacts = new List<Artifact>();

                await foreach (var evt in stream)
                {
                    renderer.RenderStreamEvent(evt, formatter);

                    if (evt.PayloadCase == StreamResponseCase.ArtifactUpdate
                        && evt.ArtifactUpdate!.Artifact != null)
                    {
                        allArtifacts.Add(evt.ArtifactUpdate.Artifact);
                    }
                }

                if (!string.IsNullOrEmpty(saveArtifacts) && allArtifacts.Count > 0)
                    await ArtifactSaver.SaveArtifactsAsync(allArtifacts, saveArtifacts);
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
