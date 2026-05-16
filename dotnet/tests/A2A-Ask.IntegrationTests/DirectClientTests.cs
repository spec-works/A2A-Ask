using A2A;
using A2AAsk.Commands;
using TestAgentServer;
using Xunit;

namespace A2AAsk.IntegrationTests;

[Collection("TestServer")]
public class DirectClientTests
{
    private readonly TestServerFixture _fixture;

    public DirectClientTests(TestServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateClientAsync_DirectUrl_DoesNotFetchAgentCard()
    {
        RequestCaptureState.Reset();

        var client = await CommonOptions.CreateClientAsync(
            $"{_fixture.BaseAddress}/direct-only",
            _fixture.CreateClient(),
            "1.0");

        var response = await client.SendMessageAsync(new SendMessageRequest
        {
            Message = new Message
            {
                Role = Role.User,
                MessageId = Guid.NewGuid().ToString("N"),
                Parts = [Part.FromText("hello")]
            }
        });

        Assert.Equal(SendMessageResponseCase.Message, response.PayloadCase);
        Assert.Equal(0, RequestCaptureState.DirectCardFetchCount);
        Assert.Equal(A2AMethods.SendMessage, RequestCaptureState.DirectLastMethod);
        Assert.Equal("1.0", RequestCaptureState.DirectLastVersionHeader);
    }

    [Fact]
    public async Task CreateClientAsync_V03DirectUrl_UsesV03MethodNamesWithoutCardFetch()
    {
        RequestCaptureState.Reset();

        var client = await CommonOptions.CreateClientAsync(
            $"{_fixture.BaseAddress}/v03-direct",
            _fixture.CreateClient(),
            "0.3");

        var sendResponse = await client.SendMessageAsync(new SendMessageRequest
        {
            Message = new Message
            {
                Role = Role.User,
                MessageId = Guid.NewGuid().ToString("N"),
                Parts = [Part.FromText("hello")]
            }
        });

        var stream = client.SendStreamingMessageAsync(new SendMessageRequest
        {
            Message = new Message
            {
                Role = Role.User,
                MessageId = Guid.NewGuid().ToString("N"),
                Parts = [Part.FromText("stream")]
            }
        });

        var streamEvent = await FirstAsync(stream);
        var task = await client.GetTaskAsync(new GetTaskRequest { Id = "task-123" });
        var canceledTask = await client.CancelTaskAsync(new CancelTaskRequest { Id = "task-123" });

        Assert.Equal(SendMessageResponseCase.Message, sendResponse.PayloadCase);
        Assert.Equal(StreamResponseCase.Message, streamEvent.PayloadCase);
        Assert.Equal("task-123", task.Id);
        Assert.Equal("task-123", canceledTask.Id);
        Assert.Equal(0, RequestCaptureState.V03CardFetchCount);
        Assert.Contains("message/send", RequestCaptureState.V03Methods);
        Assert.Contains("message/stream", RequestCaptureState.V03Methods);
        Assert.Contains("tasks/get", RequestCaptureState.V03Methods);
        Assert.Contains("tasks/cancel", RequestCaptureState.V03Methods);
    }

    private static async Task<T> FirstAsync<T>(IAsyncEnumerable<T> source)
    {
        await foreach (var item in source)
        {
            return item;
        }

        throw new InvalidOperationException("Expected at least one stream item.");
    }
}
