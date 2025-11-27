using System.CommandLine;
using PicArchiver.Commands;
using PicArchiver.Commands.IGArchiver;

namespace PicArchiver.Console;

public sealed class CmdLineApp
{
    private static readonly ICollection<Command> Commands = [
        new ArchiverCommand(),
        new ArchiverCommand(scanOnly: true),
        new MetadataCommand(),
        new IGBaseCommand()];
    
    private static readonly RootCommand RootCommand;

    static CmdLineApp()
    {
        RootCommand = new RootCommand("Picture archiver");
        foreach (var command in Commands)
        {
            RootCommand.AddCommand(command);
        }
    }

    public static int Run(string[] args)
    {
        try
        {
            return RootCommand.Invoke(args);
        }
        catch (Exception e)
        {
            System.Console.WriteLine($"Error executing command: {e.Message}");
            return -1;
        }
    }
}