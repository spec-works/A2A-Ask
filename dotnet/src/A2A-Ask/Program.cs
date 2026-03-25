using System.CommandLine;
using A2A.V0_3Compat;
using A2AAsk.Commands;

// Register v0.3 compatibility fallback so A2AClientFactory handles older agents
V03FallbackRegistration.Register();

var rootCommand = new RootCommand("A2A-Ask: Interact with A2A (Agent-to-Agent) protocol agents from the command line")
{
    Name = "a2a-ask"
};

// Global options
var outputOption = new Option<string>(
    name: "--output",
    description: "Output format",
    getDefaultValue: () => "json");
outputOption.AddCompletions("json", "text");

var prettyOption = new Option<bool>(
    name: "--pretty",
    description: "Pretty-print JSON output",
    getDefaultValue: () => false);

var verboseOption = new Option<bool>(
    aliases: ["--verbose", "-v"],
    description: "Enable verbose/debug output",
    getDefaultValue: () => false);

rootCommand.AddGlobalOption(outputOption);
rootCommand.AddGlobalOption(prettyOption);
rootCommand.AddGlobalOption(verboseOption);

// Register commands
rootCommand.AddCommand(DiscoverCommand.Create());
rootCommand.AddCommand(SendCommand.Create());
rootCommand.AddCommand(StreamCommand.Create());
rootCommand.AddCommand(TaskCommand.Create());
rootCommand.AddCommand(AuthCommand.Create());
rootCommand.AddCommand(VersionCommand.Create());

return await rootCommand.InvokeAsync(args);
