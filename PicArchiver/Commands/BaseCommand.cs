using System.CommandLine;
using System.CommandLine.IO;
using System.Diagnostics;

namespace PicArchiver.Commands;

public class BaseCommand : Command
{
    protected static bool AreColorsSupported => !(OperatingSystem.IsBrowser() || OperatingSystem.IsAndroid() ||
                                         OperatingSystem.IsIOS() || OperatingSystem.IsTvOS());
    
    public BaseCommand(string name, string? description = null) : base(name, description)
    {
    }

    protected IConsole Console { get; set; } = new SystemConsole();
    
    protected void WriteLine(string line = "") => Console.WriteLine(line);
    
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

    private int _spinnerCounter;
    private long _lastSpinnerPrintTime = Stopwatch.GetTimestamp();

    protected void WriteSpinner(string? text = null)
    {
        if (AreColorsSupported && !Console.IsOutputRedirected &&  Stopwatch.GetElapsedTime(_lastSpinnerPrintTime).TotalMilliseconds >= 333)
        {
            _spinnerCounter++;
            switch (_spinnerCounter % 4)
            {
                case 0:
                    Console.Write($" [/] {text}");
                    _spinnerCounter = 0;
                    break;
                case 1: Console.Write($" [-] {text}"); break;
                case 2: Console.Write($" [\\] {text}"); break;
                case 3: Console.Write($" [|] {text}"); break;
            }
            
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
            _lastSpinnerPrintTime =  Stopwatch.GetTimestamp();
        }
    }

    protected void BreakLine() => Console.WriteLine(string.Empty);
    
    protected void UnbreakLine()
    {
        if (!Console.IsOutputRedirected && AreColorsSupported)
            System.Console.SetCursorPosition(0, System.Console.CursorTop -1);
    }
}