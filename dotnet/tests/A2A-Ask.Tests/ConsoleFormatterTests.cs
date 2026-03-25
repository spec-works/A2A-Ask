using A2A;
using A2AAsk.Output;

namespace A2AAsk.Tests;

public class ConsoleFormatterTests
{
    [Fact]
    public void WriteJson_ProducesValidJson()
    {
        var formatter = new ConsoleFormatter("json", pretty: false);
        var output = CaptureConsoleOutput(() =>
            formatter.WriteJson(new { Name = "test", Value = 42 }));

        Assert.Contains("\"name\"", output);
        Assert.Contains("\"value\"", output);
        Assert.Contains("42", output);
    }

    [Fact]
    public void WriteJson_PrettyPrints_WhenEnabled()
    {
        var formatter = new ConsoleFormatter("json", pretty: true);
        var output = CaptureConsoleOutput(() =>
            formatter.WriteJson(new { Name = "test" }));

        Assert.Contains("\n", output);
    }

    [Fact]
    public void WriteAgentCard_TextMode_ShowsName()
    {
        var formatter = new ConsoleFormatter("text", pretty: false);
        var card = new AgentCard
        {
            Name = "Test Agent",
            Description = "A test agent",
            Version = "1.0"
        };

        var output = CaptureConsoleOutput(() =>
            formatter.WriteAgentCard(card, verbose: false));

        Assert.Contains("Agent: Test Agent", output);
        Assert.Contains("A test agent", output);
    }

    [Fact]
    public void WriteAgentCard_JsonMode_SerializesCard()
    {
        var formatter = new ConsoleFormatter("json", pretty: false);
        var card = new AgentCard
        {
            Name = "Test Agent",
            Description = "Test",
            Version = "1.0"
        };

        var output = CaptureConsoleOutput(() =>
            formatter.WriteAgentCard(card, verbose: false));

        Assert.Contains("\"name\"", output);
        Assert.Contains("Test Agent", output);
    }

    [Fact]
    public void WriteResponse_Task_ShowsTaskInfo()
    {
        var formatter = new ConsoleFormatter("text", pretty: false);
        var task = new AgentTask
        {
            Id = "task-123",
            ContextId = "ctx-456",
            Status = new A2A.TaskStatus
            {
                State = TaskState.Completed
            }
        };
        var response = new SendMessageResponse { Task = task };

        var output = CaptureConsoleOutput(() =>
            formatter.WriteResponse(response, verbose: false));

        Assert.Contains("task-123", output);
        Assert.Contains("Completed", output);
        Assert.Contains("Done", output);
    }

    [Fact]
    public void WriteResponse_Message_ShowsMessageContent()
    {
        var formatter = new ConsoleFormatter("text", pretty: false);
        var msg = new Message
        {
            Role = Role.Agent,
            Parts = [new Part { Text = "Hello from agent!" }],
            MessageId = "msg-1"
        };
        var response = new SendMessageResponse { Message = msg };

        var output = CaptureConsoleOutput(() =>
            formatter.WriteResponse(response, verbose: false));

        Assert.Contains("Hello from agent!", output);
    }

    [Fact]
    public void WriteError_ShowsMessage()
    {
        var output = CaptureErrorOutput(() =>
            ConsoleFormatter.WriteError(new InvalidOperationException("something broke"), verbose: false));

        Assert.Contains("Error: something broke", output);
    }

    [Fact]
    public void WriteError_Verbose_ShowsStackTrace()
    {
        var output = CaptureErrorOutput(() =>
            ConsoleFormatter.WriteError(new InvalidOperationException("oops"), verbose: true));

        Assert.Contains("Error: oops", output);
        Assert.Contains("Type: InvalidOperationException", output);
    }

    [Fact]
    public void WriteListTasksNotSupported_TextMode()
    {
        var formatter = new ConsoleFormatter("text", pretty: false);
        var output = CaptureConsoleOutput(() => formatter.WriteListTasksNotSupported());

        Assert.Contains("not available", output);
    }

    private static string CaptureConsoleOutput(Action action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static string CaptureErrorOutput(Action action)
    {
        var original = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            action();
            return writer.ToString();
        }
        finally
        {
            Console.SetError(original);
        }
    }
}
