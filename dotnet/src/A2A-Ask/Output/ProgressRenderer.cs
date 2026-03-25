using A2A;

namespace A2AAsk.Output;

/// <summary>
/// Renders streaming events to the console with progress indicators.
/// </summary>
public class ProgressRenderer
{
    private readonly string _mode;
    private string? _lastTaskId;

    public ProgressRenderer(string mode)
    {
        _mode = mode;
    }

    public void RenderStreamEvent(StreamResponse evt, ConsoleFormatter formatter)
    {
        if (_mode == "json")
        {
            formatter.WriteStreamEvent(evt);
            return;
        }

        switch (evt.PayloadCase)
        {
            case StreamResponseCase.Task:
            {
                var task = evt.Task!;
                _lastTaskId = task.Id;
                var taskIcon = GetStateIcon(task.Status.State);
                Console.WriteLine($"{taskIcon} Task started: {task.Id} [{task.Status.State}]");
                break;
            }

            case StreamResponseCase.Message:
            {
                var message = evt.Message!;
                Console.Write("[AGENT] ");
                foreach (var part in message.Parts)
                {
                    if (part.ContentCase == PartContentCase.Text)
                        Console.Write(part.Text);
                }
                Console.WriteLine();
                break;
            }

            case StreamResponseCase.StatusUpdate:
            {
                var statusUpdate = evt.StatusUpdate!;
                var icon = GetStateIcon(statusUpdate.Status.State);
                var stateText = statusUpdate.Status.State.ToString();

                Console.Write($"\r{icon} [{stateText}]");

                if (statusUpdate.Status.Message != null)
                {
                    foreach (var part in statusUpdate.Status.Message.Parts)
                    {
                        if (part.ContentCase == PartContentCase.Text)
                            Console.Write($" {part.Text}");
                    }
                }
                Console.WriteLine();

                if (statusUpdate.Status.State is TaskState.InputRequired)
                {
                    Console.WriteLine();
                    Console.WriteLine("InputRequired Agent requires additional input.");
                    Console.WriteLine($"  Use: a2a-ask send <url> --task-id {statusUpdate.TaskId} --message \"<your response>\"");
                }
                else if (statusUpdate.Status.State is TaskState.AuthRequired)
                {
                    Console.WriteLine();
                    Console.WriteLine("AuthRequired Agent requires authentication to proceed.");
                    Console.WriteLine($"  Use: a2a-ask auth login <url>");
                }
                break;
            }

            case StreamResponseCase.ArtifactUpdate:
            {
                var artifactUpdate = evt.ArtifactUpdate!;
                var artifact = artifactUpdate.Artifact;
                if (artifact != null)
                {
                    foreach (var part in artifact.Parts)
                    {
                        if (part.ContentCase == PartContentCase.Text)
                        {
                            Console.Write(part.Text);
                        }
                        else if (part.ContentCase == PartContentCase.Url)
                        {
                            Console.WriteLine($"Attachment Artifact: {artifact.Name ?? artifact.ArtifactId} -> {part.Url}");
                        }
                        else if (part.ContentCase == PartContentCase.Raw)
                        {
                            Console.WriteLine($"Binary Artifact: {artifact.Name ?? artifact.ArtifactId} ({part.Raw!.Length} bytes, {part.MediaType ?? "binary"})");
                        }
                    }

                    if (artifactUpdate.LastChunk)
                        Console.WriteLine();
                }
                break;
            }
        }
    }

    private static string GetStateIcon(TaskState state) => state switch
    {
        TaskState.Completed => "Done",
        TaskState.Failed => "Failed",
        TaskState.Working => "Working",
        TaskState.Submitted => "Submitted",
        TaskState.InputRequired => "InputRequired",
        TaskState.AuthRequired => "AuthRequired",
        TaskState.Canceled => "Canceled",
        TaskState.Rejected => "Rejected",
        _ => "Unknown"
    };
}
