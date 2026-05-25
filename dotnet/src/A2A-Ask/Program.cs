using System.CommandLine;
using A2AAsk.Commands;

var rootCommand = new RootCommand("A2A-Ask: Interact with A2A (Agent-to-Agent) protocol agents from the command line")
{
    Name = "a2a-ask"
};

// Global options
rootCommand.AddGlobalOption(GlobalOptions.Output);
rootCommand.AddGlobalOption(GlobalOptions.Pretty);
rootCommand.AddGlobalOption(GlobalOptions.Verbose);

// Register commands
rootCommand.AddCommand(DiscoverCommand.Create());
rootCommand.AddCommand(CatalogCommand.Create());
rootCommand.AddCommand(SendCommand.Create());
rootCommand.AddCommand(StreamCommand.Create());
rootCommand.AddCommand(TaskCommand.Create());
rootCommand.AddCommand(AuthCommand.Create());
rootCommand.AddCommand(VersionCommand.Create());

return await rootCommand.InvokeAsync(args);
