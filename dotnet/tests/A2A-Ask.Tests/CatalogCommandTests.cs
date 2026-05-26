using System.CommandLine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using A2AAsk.Catalog;
using A2AAsk.Commands;

namespace A2AAsk.Tests;

public sealed class CatalogCommandTests : IDisposable
{
    private readonly List<string> _createdFiles = [];

    public void Dispose()
    {
        foreach (var filePath in _createdFiles)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task CatalogSync_WithUpdatedAgentCard_RefreshesBridgeFile()
    {
        const string updatedCardJson = """
        {
          "name": "Weather Agent",
          "description": "Provides fresh forecasts",
          "skills": [
            {
              "id": "forecast",
              "name": "Forecast",
              "description": "Get forecasts."
            }
          ],
          "capabilities": {
            "streaming": true
          }
        }
        """;

        using var server = new TestHttpServer(request =>
        {
            Assert.Equal("GET", request.HttpMethod);
            Assert.Equal("/weather/.well-known/agent-card.json", request.Url!.AbsolutePath);

            return HttpResponseDefinition.Json(updatedCardJson, "\"etag-new\"");
        });

        var agentName = $"weather-agent-{Guid.NewGuid():N}";
        var cardUrl = new Uri(server.BaseAddress, "weather/.well-known/agent-card.json").ToString();
        var filePath = CreateBridgeFile(
            agentName,
            cardUrl,
            "\"etag-old\"",
            BridgeGenerator.ComputeSha256(Encoding.UTF8.GetBytes("{\"name\":\"Weather Agent\"}")));

        var result = await InvokeCatalogAsync("catalog", "sync", agentName, "--output", "text");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains($"- {agentName}: updated", result.StdOut, StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(result.StdErr));

        var markdown = await File.ReadAllTextAsync(filePath);
        Assert.True(FrontmatterReader.TryRead(markdown, out var frontmatter));
        Assert.NotNull(frontmatter);
        Assert.Equal("\"etag-new\"", frontmatter!.CardEtag);
        Assert.Equal(BridgeGenerator.ComputeSha256(Encoding.UTF8.GetBytes(updatedCardJson)), frontmatter.CardHash);
        Assert.Contains("`forecast`", markdown, StringComparison.Ordinal);
        Assert.Contains("Get forecasts.", markdown, StringComparison.Ordinal);
        Assert.Contains("# Weather Agent (A2A bridge)", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogSync_WithStoredEtagAndNotModifiedResponse_SendsIfNoneMatchAndLeavesBridgeUnchanged()
    {
        const string storedEtag = "\"etag-304\"";
        var agentName = $"weather-agent-{Guid.NewGuid():N}";
        CapturedRequest? capturedRequest = null;

        using var server = new TestHttpServer(request =>
        {
            capturedRequest = new CapturedRequest(
                request.HttpMethod,
                request.Url!,
                request.Headers.AllKeys
                    .Where(key => key is not null)
                    .ToDictionary(key => key!, key => request.Headers[key!]!, StringComparer.OrdinalIgnoreCase));

            return new HttpResponseDefinition(HttpStatusCode.NotModified, null, null);
        });

        var cardUrl = new Uri(server.BaseAddress, "weather/.well-known/agent-card.json").ToString();
        var filePath = CreateBridgeFile(
            agentName,
            cardUrl,
            storedEtag,
            BridgeGenerator.ComputeSha256(Encoding.UTF8.GetBytes("{\"name\":\"Weather Agent\"}")));
        var originalMarkdown = await File.ReadAllTextAsync(filePath);

        var result = await InvokeCatalogAsync("catalog", "sync", agentName, "--output", "text");

        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest!.Headers.TryGetValue("If-None-Match", out var headerValue));
        Assert.Equal(storedEtag, headerValue);
        Assert.Contains($"- {agentName}: unchanged", result.StdOut, StringComparison.Ordinal);
        Assert.Equal(originalMarkdown, await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task CatalogSync_VerboseMode_EmitsStructuredFetchTelemetryToStandardError()
    {
        const string updatedCardJson = """
        {
          "name": "Weather Agent",
          "description": "Provides fresh forecasts"
        }
        """;

        using var server = new TestHttpServer(_ => HttpResponseDefinition.Json(updatedCardJson, "\"etag-telemetry\""));

        var agentName = $"weather-agent-{Guid.NewGuid():N}";
        var cardUrl = new Uri(server.BaseAddress, "weather/.well-known/agent-card.json").ToString();
        CreateBridgeFile(
            agentName,
            cardUrl,
            "\"etag-old\"",
            BridgeGenerator.ComputeSha256(Encoding.UTF8.GetBytes("{\"name\":\"Weather Agent\"}")));

        var result = await InvokeCatalogAsync("catalog", "sync", agentName, "--output", "text", "--verbose");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(cardUrl, result.StdErr, StringComparison.Ordinal);
        var telemetryLine = Assert.Single(result.StdErr.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        using var telemetry = System.Text.Json.JsonDocument.Parse(telemetryLine);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, telemetry.RootElement.ValueKind);
    }

    private string CreateBridgeFile(string agentName, string cardUrl, string? cardEtag, string? cardHash)
    {
        var filePath = Path.Combine(GetCopilotAgentsDirectory(), $"{agentName}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var markdown = new BridgeGenerator().GenerateMarkdown(new BridgeTemplateModel(
            agentName,
            "weather@catalog",
            "Weather Agent",
            new BridgeAgentCard("Weather Agent", "Old description", [], RequiresAuthentication: false, SupportsStreaming: true),
            new BridgeRemoteAgentMetadata(
                "https://catalog.example/.well-known/ai-catalog.json",
                "weather",
                cardUrl,
                cardEtag,
                cardHash,
                "2026-05-24T13:09:00Z")));

        File.WriteAllText(filePath, markdown);
        _createdFiles.Add(filePath);
        return filePath;
    }

    private static async Task<CommandInvocationResult> InvokeCatalogAsync(params string[] args)
    {
        var rootCommand = new RootCommand("A2A-Ask test root")
        {
            Name = "a2a-ask"
        };
        rootCommand.AddGlobalOption(GlobalOptions.Output);
        rootCommand.AddGlobalOption(GlobalOptions.Pretty);
        rootCommand.AddGlobalOption(GlobalOptions.Verbose);
        rootCommand.AddCommand(CatalogCommand.Create());

        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errorWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            var exitCode = await rootCommand.InvokeAsync(args);
            return new CommandInvocationResult(exitCode, outWriter.ToString(), errorWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static string GetCopilotAgentsDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".copilot",
        "agents");

    private sealed record CommandInvocationResult(int ExitCode, string StdOut, string StdErr);

    private sealed record CapturedRequest(string Method, Uri Url, IReadOnlyDictionary<string, string> Headers);

    private sealed record HttpResponseDefinition(HttpStatusCode StatusCode, string? Body, string? ContentType, IReadOnlyDictionary<string, string>? Headers = null)
    {
        public static HttpResponseDefinition Json(string body, string? etag = null)
        {
            var headers = string.IsNullOrWhiteSpace(etag)
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ETag"] = etag
                };

            return new HttpResponseDefinition(HttpStatusCode.OK, body, "application/json", headers);
        }
    }

    private sealed class TestHttpServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Func<HttpListenerRequest, HttpResponseDefinition> _handler;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _serverTask;

        public TestHttpServer(Func<HttpListenerRequest, HttpResponseDefinition> handler)
        {
            _handler = handler;
            var port = GetAvailablePort();
            BaseAddress = new Uri($"http://127.0.0.1:{port}/");
            _listener = new HttpListener();
            _listener.Prefixes.Add(BaseAddress.ToString());
            _listener.Start();
            _serverTask = Task.Run(ServeAsync);
        }

        public Uri BaseAddress { get; }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _listener.Stop();
            _listener.Close();

            try
            {
                _serverTask.GetAwaiter().GetResult();
            }
            catch (HttpListenerException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task ServeAsync()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (HttpListenerException) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (_cancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }

                var response = _handler(context.Request);
                context.Response.StatusCode = (int)response.StatusCode;

                if (!string.IsNullOrWhiteSpace(response.ContentType))
                {
                    context.Response.ContentType = response.ContentType;
                }

                if (response.Headers is not null)
                {
                    foreach (var (key, value) in response.Headers)
                    {
                        context.Response.Headers[key] = value;
                    }
                }

                if (!string.IsNullOrEmpty(response.Body))
                {
                    var bytes = Encoding.UTF8.GetBytes(response.Body);
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes);
                }
                else
                {
                    context.Response.ContentLength64 = 0;
                }

                context.Response.Close();
            }
        }

        private static int GetAvailablePort()
        {
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
    }
}