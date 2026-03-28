using System.CommandLine;
using System.CommandLine.Invocation;
using A2A;
using A2AAsk.Auth;
using A2AAsk.Output;

namespace A2AAsk.Commands;

public static class TaskCommand
{
    public static Command Create()
    {
        var taskCommand = new Command("task", "Manage A2A tasks");
        taskCommand.AddCommand(CreateGetCommand());
        taskCommand.AddCommand(CreateListCommand());
        taskCommand.AddCommand(CreateCancelCommand());
        return taskCommand;
    }

    private static Command CreateGetCommand()
    {
        var urlArgument = new Argument<string>("url", "Agent endpoint URL");
        var taskIdOption = new Option<string>(aliases: ["--task-id", "-t"], description: "Task ID") { IsRequired = true };
        var historyLengthOption = new Option<int?>("--history-length", "Max history messages to include");
        var authTokenOption = CommonOptions.AuthToken();
        var authHeaderOption = CommonOptions.AuthHeader();
        var apiKeyOption = CommonOptions.ApiKey();
        var apiKeyHeaderOption = CommonOptions.ApiKeyHeader();
        var tenantOption = CommonOptions.Tenant();

        var command = new Command("get", "Get the current state of a task")
        {
            urlArgument, taskIdOption, historyLengthOption,
            authTokenOption, authHeaderOption, apiKeyOption, apiKeyHeaderOption, tenantOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var taskId = context.ParseResult.GetValueForOption(taskIdOption)!;
            var historyLength = context.ParseResult.GetValueForOption(historyLengthOption);
            var authToken = context.ParseResult.GetValueForOption(authTokenOption);
            var authHeader = context.ParseResult.GetValueForOption(authHeaderOption);
            var apiKey = context.ParseResult.GetValueForOption(apiKeyOption);
            var apiKeyHeader = context.ParseResult.GetValueForOption(apiKeyHeaderOption);
            var tenant = context.ParseResult.GetValueForOption(tenantOption);
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
                    url, authToken: authToken, authHeader: authHeader,
                    apiKey: apiKey, apiKeyHeader: apiKeyHeader);

                var ct = context.GetCancellationToken();
                var client = await CommonOptions.CreateClientAsync(
                    url, httpClient, ct);

                var request = new GetTaskRequest { Id = taskId };
                if (historyLength.HasValue)
                    request.HistoryLength = historyLength.Value;

                var task = await client.GetTaskAsync(request, ct);
                var formatter = new ConsoleFormatter(output, pretty);
                formatter.WriteTask(task, verbose);
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateListCommand()
    {
        var urlArgument = new Argument<string>("url", "Agent endpoint URL");
        var contextIdOption = new Option<string?>("--context-id", "Filter by context ID");
        var statusOption = new Option<string?>("--status", "Filter by task state");
        var pageSizeOption = new Option<int?>("--page-size", "Results per page (default: 50)");
        var pageTokenOption = new Option<string?>("--page-token", "Pagination cursor token");
        var authTokenOption = CommonOptions.AuthToken();
        var authHeaderOption = CommonOptions.AuthHeader();
        var apiKeyOption = CommonOptions.ApiKey();
        var apiKeyHeaderOption = CommonOptions.ApiKeyHeader();
        var tenantOption = CommonOptions.Tenant();

        var command = new Command("list", "List tasks with optional filtering")
        {
            urlArgument, contextIdOption, statusOption, pageSizeOption, pageTokenOption,
            authTokenOption, authHeaderOption, apiKeyOption,
            apiKeyHeaderOption, tenantOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var contextId = context.ParseResult.GetValueForOption(contextIdOption);
            var status = context.ParseResult.GetValueForOption(statusOption);
            var pageSize = context.ParseResult.GetValueForOption(pageSizeOption);
            var pageToken = context.ParseResult.GetValueForOption(pageTokenOption);
            var authToken = context.ParseResult.GetValueForOption(authTokenOption);
            var authHeader = context.ParseResult.GetValueForOption(authHeaderOption);
            var apiKey = context.ParseResult.GetValueForOption(apiKeyOption);
            var apiKeyHeader = context.ParseResult.GetValueForOption(apiKeyHeaderOption);
            var tenant = context.ParseResult.GetValueForOption(tenantOption);
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
                    url, authToken: authToken, authHeader: authHeader,
                    apiKey: apiKey, apiKeyHeader: apiKeyHeader);

                var ct = context.GetCancellationToken();
                var client = await CommonOptions.CreateClientAsync(
                    url, httpClient, ct);

                var request = new ListTasksRequest();
                if (!string.IsNullOrEmpty(contextId))
                    request.ContextId = contextId;
                if (pageSize.HasValue)
                    request.PageSize = pageSize.Value;
                if (!string.IsNullOrEmpty(pageToken))
                    request.PageToken = pageToken;

                var result = await client.ListTasksAsync(request, ct);
                var formatter = new ConsoleFormatter(output, pretty);
                formatter.WriteJson(result);
            }
            catch (Exception ex)
            {
                ConsoleFormatter.WriteError(ex, verbose);
                context.ExitCode = 1;
            }
        });

        return command;
    }

    private static Command CreateCancelCommand()
    {
        var urlArgument = new Argument<string>("url", "Agent endpoint URL");
        var taskIdOption = new Option<string>(aliases: ["--task-id", "-t"], description: "Task ID to cancel") { IsRequired = true };
        var authTokenOption = CommonOptions.AuthToken();
        var authHeaderOption = CommonOptions.AuthHeader();
        var apiKeyOption = CommonOptions.ApiKey();
        var apiKeyHeaderOption = CommonOptions.ApiKeyHeader();
        var tenantOption = CommonOptions.Tenant();

        var command = new Command("cancel", "Cancel a running task")
        {
            urlArgument, taskIdOption,
            authTokenOption, authHeaderOption, apiKeyOption, apiKeyHeaderOption, tenantOption
        };

        command.SetHandler(async (InvocationContext context) =>
        {
            var url = context.ParseResult.GetValueForArgument(urlArgument);
            var taskId = context.ParseResult.GetValueForOption(taskIdOption)!;
            var authToken = context.ParseResult.GetValueForOption(authTokenOption);
            var authHeader = context.ParseResult.GetValueForOption(authHeaderOption);
            var apiKey = context.ParseResult.GetValueForOption(apiKeyOption);
            var apiKeyHeader = context.ParseResult.GetValueForOption(apiKeyHeaderOption);
            var tenant = context.ParseResult.GetValueForOption(tenantOption);
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
                    url, authToken: authToken, authHeader: authHeader,
                    apiKey: apiKey, apiKeyHeader: apiKeyHeader);

                var ct = context.GetCancellationToken();
                var client = await CommonOptions.CreateClientAsync(
                    url, httpClient, ct);

                var request = new CancelTaskRequest { Id = taskId };
                var task = await client.CancelTaskAsync(request, ct);
                var formatter = new ConsoleFormatter(output, pretty);
                formatter.WriteTask(task, verbose);
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


