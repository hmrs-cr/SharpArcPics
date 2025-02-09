using System.CommandLine;
using System.CommandLine.IO;

namespace PicArchiver.Commands;

public class BaseCommand : Command
{
    private bool AreColorsSupported => !(OperatingSystem.IsBrowser() || OperatingSystem.IsAndroid() ||
                                         OperatingSystem.IsIOS() || OperatingSystem.IsTvOS());
    
    public BaseCommand(string name, string? description = null) : base(name, description)
    {
    }

    protected IConsole Console { get; } = new SystemConsole();
    
    protected void WriteLine(string line) => Console.WriteLine(line);
    
    protected void Write(string text) => Console.Write(text);

    protected void Write(ConsoleColor color, string text)
    {
        if (!Console.IsOutputRedirected && AreColorsSupported)
        {
            System.Console.ForegroundColor = color;
            Write(text);
            System.Console.ResetColor();
            return;
        }
        
        Write(text);
    }

    protected void WriteLine(ConsoleColor color, string line)
    {
        if (!Console.IsOutputRedirected && AreColorsSupported)
        {
            System.Console.ForegroundColor = color;
            WriteLine(line);
            System.Console.ResetColor();
            return;
        }
        
        WriteLine(line);
    }
    
    protected void WriteErrorLine(string line)
    {
        if (!Console.IsOutputRedirected && AreColorsSupported)
        {
            System.Console.ForegroundColor = ConsoleColor.Red;
            WriteLine(line);
            System.Console.ResetColor();
            return;
        }
        
        WriteLine(line);
    }
}