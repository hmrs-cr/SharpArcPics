using System.Diagnostics;
using System.Text.Json;

namespace PicArchiver.Extensions;

public static class ProcessExtensions
{
    public static Process CreateProcessCommand(string command, string arguments) => new()
    {
        StartInfo =
        {
            FileName = command,
            UseShellExecute = false,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }
    };

    public static int StartAndWaitUntilExit(this Process process)
    {
        process.Start();
        process.WaitForExit();
        return process.ExitCode;
    }

    public static int ExecuteProcessCommand(string command, string arguments)
    {
        var process = CreateProcessCommand(command, arguments);
        return process.StartAndWaitUntilExit();
    }
}
