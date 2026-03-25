using A2A;
using A2AAsk.Output;

namespace A2AAsk.Tests;

public class ProgressRendererTests
{
    [Fact]
    public void RenderStreamEvent_JsonMode_WritesJson()
    {
        var renderer = new ProgressRenderer("json");
        var formatter = new ConsoleFormatter("json", pretty: false);
        var evt = new StreamResponse
        {
            StatusUpdate = new TaskStatusUpdateEvent
            {
                TaskId = "task-1",
                Status = new A2A.TaskStatus { State = TaskState.Working }
            }
        };

        var output = CaptureConsoleOutput(() =>
            renderer.RenderStreamEvent(evt, formatter));

        Assert.Contains("task-1", output);
    }

    [Fact]
    public void RenderStreamEvent_TextMode_StatusUpdate_ShowsIcon()
    {
        var renderer = new ProgressRenderer("text");
        var formatter = new ConsoleFormatter("text", pretty: false);
        var evt = new StreamResponse
        {
            StatusUpdate = new TaskStatusUpdateEvent
            {
                TaskId = "task-1",
                Status = new A2A.TaskStatus { State = TaskState.Working }
            }
        };

        var output = CaptureConsoleOutput(() =>
            renderer.RenderStreamEvent(evt, formatter));

        Assert.Contains("Working", output);
    }

    [Fact]
    public void RenderStreamEvent_TextMode_InputRequired_ShowsHint()
    {
        var renderer = new ProgressRenderer("text");
        var formatter = new ConsoleFormatter("text", pretty: false);
        var evt = new StreamResponse
        {
            StatusUpdate = new TaskStatusUpdateEvent
            {
                TaskId = "task-42",
                Status = new A2A.TaskStatus { State = TaskState.InputRequired }
            }
        };

        var output = CaptureConsoleOutput(() =>
            renderer.RenderStreamEvent(evt, formatter));

        Assert.Contains("InputRequired", output);
        Assert.Contains("additional input", output);
        Assert.Contains("--task-id task-42", output);
    }

    [Fact]
    public void RenderStreamEvent_TextMode_AuthRequired_ShowsHint()
    {
        var renderer = new ProgressRenderer("text");
        var formatter = new ConsoleFormatter("text", pretty: false);
        var evt = new StreamResponse
        {
            StatusUpdate = new TaskStatusUpdateEvent
            {
                TaskId = "task-99",
                Status = new A2A.TaskStatus { State = TaskState.AuthRequired }
            }
        };

        var output = CaptureConsoleOutput(() =>
            renderer.RenderStreamEvent(evt, formatter));

        Assert.Contains("AuthRequired", output);
        Assert.Contains("authentication", output);
    }

    [Fact]
    public void RenderStreamEvent_TextMode_AgentMessage_ShowsText()
    {
        var renderer = new ProgressRenderer("text");
        var formatter = new ConsoleFormatter("text", pretty: false);
        var evt = new StreamResponse
        {
            Message = new Message
            {
                Role = Role.Agent,
                Parts = [new Part { Text = "Hello world" }],
                MessageId = "msg-1"
            }
        };

        var output = CaptureConsoleOutput(() =>
            renderer.RenderStreamEvent(evt, formatter));

        Assert.Contains("[AGENT]", output);
        Assert.Contains("Hello world", output);
    }

    [Fact]
    public void RenderStreamEvent_TextMode_ArtifactUpdate_StreamsText()
    {
        var renderer = new ProgressRenderer("text");
        var formatter = new ConsoleFormatter("text", pretty: false);
        var evt = new StreamResponse
        {
            ArtifactUpdate = new TaskArtifactUpdateEvent
            {
                TaskId = "task-1",
                Artifact = new Artifact
                {
                    ArtifactId = "art-1",
                    Name = "report",
                    Parts = [new Part { Text = "Report content here" }]
                }
            }
        };

        var output = CaptureConsoleOutput(() =>
            renderer.RenderStreamEvent(evt, formatter));

        Assert.Contains("Report content here", output);
    }

    [Fact]
    public void RenderStreamEvent_TextMode_Completed_ShowsCheckmark()
    {
        var renderer = new ProgressRenderer("text");
        var formatter = new ConsoleFormatter("text", pretty: false);
        var evt = new StreamResponse
        {
            StatusUpdate = new TaskStatusUpdateEvent
            {
                TaskId = "task-1",
                Status = new A2A.TaskStatus { State = TaskState.Completed }
            }
        };

        var output = CaptureConsoleOutput(() =>
            renderer.RenderStreamEvent(evt, formatter));

        Assert.Contains("Done", output);
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
}
