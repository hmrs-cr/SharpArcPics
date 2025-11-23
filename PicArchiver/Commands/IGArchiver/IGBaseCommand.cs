using System.CommandLine;

namespace PicArchiver.Commands.IGArchiver;

public class IGBaseCommand : Command
{
    private static ICollection<Command> Commands { get; } =
    [
        new ScanCommand(),
        new RemoveDuplicatesCommand(),
        new GetRandomCommand()
    ];
    
    protected IGBaseCommand(string name, string description) : base($"{name}", description)
    {
    }

    public IGBaseCommand() : base("ig", "Archives IG pictures.")
    {
        foreach (var command in Commands)
        {
            this.AddCommand(command);
        }
    }
}