using System.Net.Http.Headers;
using A2A;
using Xunit;

namespace A2AAsk.IntegrationTests;

/// <summary>
/// Tests multi-turn conversation flow (input-required state) and
/// mid-task auth escalation (auth-required state).
/// </summary>
[Collection("TestServer")]
public class MultiTurnTests
{
    private readonly TestServerFixture _fixture;

    public MultiTurnTests(TestServerFixture fixture) => _fixture = fixture;

    private A2AClient CreateClient(string path, HttpClient? httpClient = null)
    {
        var client = httpClient ?? _fixture.Client;
        return new A2AClient(new Uri(client.BaseAddress!, path), client);
    }

    private static SendMessageRequest MakeRequest(string text, string? taskId = null) => new()
    {
        Message = new Message
        {
            Role = Role.User,
            MessageId = Guid.NewGuid().ToString("N"),
            TaskId = taskId,
            Parts = [Part.FromText(text)],
        },
        Configuration = new SendMessageConfiguration { Blocking = true },
    };

    [Fact]
    public async Task InputRequired_FirstMessage_ReturnsInputRequiredState()
    {
        var client = CreateClient("/input-required");
        var response = await client.SendMessageAsync(MakeRequest("start"));

        Assert.NotNull(response.Task);
        Assert.Equal(TaskState.InputRequired, response.Task.Status.State);

        var question = response.Task.Status.Message?.Parts
            ?.FirstOrDefault(p => p.Text is not null)?.Text;
        Assert.Contains("name", question, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InputRequired_FollowUp_CompletesWithGreeting()
    {
        var client = CreateClient("/input-required");

        // First message → input-required
        var first = await client.SendMessageAsync(MakeRequest("start"));
        Assert.NotNull(first.Task);
        Assert.Equal(TaskState.InputRequired, first.Task.Status.State);
        var taskId = first.Task.Id;

        // Follow-up with the task ID → completed
        var second = await client.SendMessageAsync(MakeRequest("Alice", taskId));
        Assert.NotNull(second.Task);
        Assert.Equal(TaskState.Completed, second.Task.Status.State);

        var greeting = second.Task.Status.Message?.Parts
            ?.FirstOrDefault(p => p.Text is not null)?.Text;
        Assert.Contains("Alice", greeting);
    }

    [Fact]
    public async Task AuthRequired_WithoutBearer_ReturnsAuthRequiredState()
    {
        var client = CreateClient("/auth-required");
        var response = await client.SendMessageAsync(MakeRequest("hello"));

        Assert.NotNull(response.Task);
        Assert.Equal(TaskState.AuthRequired, response.Task.Status.State);
    }

    [Fact]
    public async Task AuthRequired_WithBearer_ReturnsEcho()
    {
        var httpClient = _fixture.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "any-valid-token");
        var client = CreateClient("/auth-required", httpClient);

        var response = await client.SendMessageAsync(MakeRequest("hello"));

        // With bearer present, the agent echoes back
        Assert.NotNull(response);
        string? text = null;
        if (response.Message is { } msg)
            text = msg.Parts.FirstOrDefault(p => p.Text is not null)?.Text;
        else if (response.Task is { } task)
            text = task.Status?.Message?.Parts?.FirstOrDefault(p => p.Text is not null)?.Text;

        Assert.Contains("Echo", text);
        Assert.Contains("hello", text);
    }
}
