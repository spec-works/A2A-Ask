using System.CommandLine;
using System.Reflection;

namespace A2AAsk.Commands;

public static class VersionCommand
{
    public static Command Create()
    {
        var command = new Command("version", "Display A2A-Ask version information");

        command.SetHandler(() =>
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? "unknown";

            Console.WriteLine($"a2a-ask {version}");
        });

        return command;
    }
}
