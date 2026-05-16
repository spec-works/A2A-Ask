using System.Collections.Concurrent;

namespace TestAgentServer;

public static class RequestCaptureState
{
    private static readonly ConcurrentQueue<string> s_v03Methods = new();

    public static int DirectCardFetchCount { get; private set; }

    public static string? DirectLastMethod { get; private set; }

    public static string? DirectLastVersionHeader { get; private set; }

    public static int V03CardFetchCount { get; private set; }

    public static IReadOnlyCollection<string> V03Methods => s_v03Methods.ToArray();

    public static string? V03LastVersionHeader { get; private set; }

    public static void Reset()
    {
        DirectCardFetchCount = 0;
        DirectLastMethod = null;
        DirectLastVersionHeader = null;
        V03CardFetchCount = 0;
        V03LastVersionHeader = null;

        while (s_v03Methods.TryDequeue(out _))
        {
        }
    }

    public static void RecordDirectCardFetch() => DirectCardFetchCount++;

    public static void RecordDirectRequest(string? method, string? versionHeader)
    {
        DirectLastMethod = method;
        DirectLastVersionHeader = versionHeader;
    }

    public static void RecordV03CardFetch() => V03CardFetchCount++;

    public static void RecordV03Request(string method, string? versionHeader)
    {
        s_v03Methods.Enqueue(method);
        V03LastVersionHeader = versionHeader;
    }
}
