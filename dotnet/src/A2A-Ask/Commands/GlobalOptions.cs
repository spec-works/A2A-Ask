using System.CommandLine;
using System.CommandLine.Invocation;

namespace A2AAsk.Commands;

internal static class GlobalOptions
{
    public static Option<string> Output { get; } = CreateOutputOption();

    public static Option<bool> Pretty { get; } = new(
        name: "--pretty",
        description: "Pretty-print JSON output",
        getDefaultValue: () => false);

    public static Option<bool> Verbose { get; } = new(
        aliases: ["--verbose", "-v"],
        description: "Enable verbose/debug output",
        getDefaultValue: () => false);

    public static GlobalOptionValues GetGlobalOptions(this InvocationContext context) => new(
        context.ParseResult.GetValueForOption(Output)!,
        context.ParseResult.GetValueForOption(Pretty),
        context.ParseResult.GetValueForOption(Verbose));

    private static Option<string> CreateOutputOption()
    {
        var option = new Option<string>(
            name: "--output",
            description: "Output format",
            getDefaultValue: () => "json");
        option.AddCompletions("json", "text");
        return option;
    }
}

internal readonly record struct GlobalOptionValues(string Output, bool Pretty, bool Verbose);
