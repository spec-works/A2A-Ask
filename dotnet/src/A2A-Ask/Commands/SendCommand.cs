using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using A2A;
using A2AAsk.Auth;
using A2AAsk.Output;

namespace A2AAsk.Commands;

public static class SendCommand
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
            description: "Structured JSON data to include as a message part");

        var taskIdOption = CommonOptions.TaskId();
        var contextIdOption = CommonOptions.ContextId();

        var messageIdOption = new Option<string?>(
            name: "--message-id",
            description: "Custom message ID (defaults to auto-generated UUID)");

        var acceptOption = new Option<string?>(
            name: "--accept",
            description: "Accepted output modes (comma-separated media types)");

        var returnImmediatelyOption = new Option<bool>(
            name: "--return-immediately",
            description: "Return immediately without waiting for task completion",
            getDefaultValue: () => false);

        var historyLengthOption = new Option<int?>(
            name: "--history-length",
            description: "Maximum number of history messages to include in response");

        var authTokenOption = CommonOptions.AuthToken();
        var authHeaderOption = CommonOptions.AuthHeader();
        var apiKeyOption = CommonOptions.ApiKey();
        var apiKeyHeaderOption = CommonOptions.ApiKeyHeader();
        var authUserOption = CommonOptions.AuthUser();
        var authPasswordOption = CommonOptions.AuthPassword();
        var clientIdOption = CommonOptions.ClientId();
        var clientSecretOption = CommonOptions.ClientSecret();
        var bindingOption = CommonOptions.Binding();
        var a2aVersionOption = CommonOptions.A2AVersion();
        var tenantOption = CommonOptions.Tenant();
        var saveArtifactsOption = CommonOptions.SaveArtifacts();

        var command = new Command("send", "Send a message to an A2A agent")
        {
            urlArgument,
            messageOption,
            fileOption,
            dataOption,
            taskIdOption,
            contextIdOption,
            messageIdOption,
            acceptOption,
            returnImmediatelyOption,
            historyLengthOption,
            authTokenOption,
            authHeaderOption,
            apiKeyOption,
            apiKeyHeaderOption,
            authUserOption,
            authPasswordOption,
            clientIdOption,
            clientSecretOption,
            bindingOption,
            a2aVersionOption,
            tenantOption,
            saveArtifactsOption
        };

        command.AddValidator(result =>
        {
            var message = result.GetValueForOption(messageOption);
            var file = result.GetValueForOption(fileOption);
            var data = result.GetValueForOption(dataOption);
            if (string.IsNullOrEmpty(message) && file == null && string.IsNullOrEmpty(data))
            {
                result.ErrorMessage = "At least one of --message, --file, or --data is required.";
            }
        });

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var message = context.ParseResult.GetValueForOption(messageOption);
            var file = context.ParseResult.GetValueForOption(fileOption);
            var data = context.ParseResult.GetValueForOption(dataOption);
            var taskId = context.ParseResult.GetValueForOption(taskIdOption);
            var contextId = context.ParseResult.GetValueForOption(contextIdOption);
            var messageId = context.ParseResult.GetValueForOption(messageIdOption) ?? Guid.NewGuid().ToString();
            var accept = context.ParseResult.GetValueForOption(acceptOption);
            var returnImmediately = context.ParseResult.GetValueForOption(returnImmediatelyOption);
            var historyLength = context.ParseResult.GetValueForOption(historyLengthOption);
            var authToken = context.ParseResult.GetValueForOption(authTokenOption);
            var authHeader = context.ParseResult.GetValueForOption(authHeaderOption);
            var apiKey = context.ParseResult.GetValueForOption(apiKeyOption);
            var apiKeyHeader = context.ParseResult.GetValueForOption(apiKeyHeaderOption);
            var authUser = context.ParseResult.GetValueForOption(authUserOption);
            var authPassword = context.ParseResult.GetValueForOption(authPasswordOption);
            var clientId = context.ParseResult.GetValueForOption(clientIdOption);
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOption);
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
                var httpClient = await AuthConfigurator.CreateHttpClientWithStoredTokenAsync(
                    url,
                    authToken: authToken,
                    authHeader: authHeader,
                    apiKey: apiKey,
                    apiKeyHeader: apiKeyHeader,
                    authUser: authUser,
                    authPassword: authPassword,
                    tenant: tenant);

                var client = await CommonOptions.CreateClientAsync(
                    url, httpClient, context.GetCancellationToken());

                var parts = BuildParts(message, file, data);

                var msg = new Message
                {
                    Role = Role.User,
                    MessageId = messageId,
                    Parts = parts,
                    TaskId = taskId,
                    ContextId = contextId
                };

                var config = new SendMessageConfiguration();
                if (!string.IsNullOrEmpty(accept))
                    config.AcceptedOutputModes = accept.Split(',', StringSplitOptions.TrimEntries).ToList();
                if (returnImmediately)
                    config.Blocking = false;
                if (historyLength.HasValue)
                    config.HistoryLength = historyLength.Value;

                var request = new SendMessageRequest
                {
                    Message = msg,
                    Configuration = config
                };

                var response = await client.SendMessageAsync(request, context.GetCancellationToken());
                var formatter = new ConsoleFormatter(output, pretty);
                formatter.WriteResponse(response, verbose);

                if (!string.IsNullOrEmpty(saveArtifacts)
                    && response.PayloadCase == SendMessageResponseCase.Task
                    && response.Task!.Artifacts != null)
                {
                    await ArtifactSaver.SaveArtifactsAsync(response.Task.Artifacts, saveArtifacts);
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

    internal static List<Part> BuildParts(string? message, FileInfo? file, string? data)
    {
        var parts = new List<Part>();
        if (!string.IsNullOrEmpty(message))
            parts.Add(new Part { Text = message });

        if (file != null)
        {
            var bytes = File.ReadAllBytes(file.FullName);
            parts.Add(new Part
            {
                Raw = bytes,
                Filename = file.Name,
                MediaType = GetMediaType(file.Name)
            });
        }

        if (!string.IsNullOrEmpty(data))
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(data);
            parts.Add(new Part { Data = jsonElement });
        }
        return parts;
    }

    private static string GetMediaType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext switch
        {
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".html" => "text/html",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }
}
