using System.CommandLine;
using PicArchiver.Core;
using PicArchiver.Core.Configs;
using PicArchiver.Extensions;

namespace PicArchiver.Commands;

public class ArchiverCommand : BaseCommand 
{
    const ConsoleColor HeaderColor = ConsoleColor.Cyan;
    
    internal ArchiverCommand(bool scanOnly = false) : base(
        name: scanOnly ? "scan" :
                         "archive", 
        
        description: scanOnly ? "Scans source folder for possible changes according to destination folder and config" : 
                                "Archives files from source to destination applying logic defined by config.")
    {
        var sourceOption = new Option<string>(
            name: "--source",
            description: "The source folder containing the pictures to archive.");
        
        var destinationOption = new Option<string>(
            name: "--destination",
            description: "The destination folder to archive the pictures.");
        
        var configNameOption = new Option<string>(
            name: "--config",
            description: $"The config  to use. Could be any of {string.Join(',', DefaultFileArchiveConfig.GetConfigNames())} " +
                         $"or a file containing the configuration in json format.");
        
        
        AddOption(sourceOption);
        AddOption(destinationOption);
        AddOption(configNameOption);

        Action<string, string, string> handler = scanOnly ? ScanFiles : ArchiveFiles;
        this.SetHandler(handler, sourceOption, destinationOption, configNameOption);
    }

    private void ScanFiles(string source, string destination, string? configName)
    {
        try
        {
            var isValid = TryGetConfigAndSourceFolders(source, destination, configName, out var config, out var sourceFolders);
            if (!isValid || config == null)
                return;

            Write("Scanning ");
            PrintFolderList(sourceFolders);
            WriteLine();
            
            var folderArchiver = new MultiFolderArchiver(sourceFolders, config);
            PrintResults(folderArchiver.ScanAll());
        }
        catch (Exception e)
        {
            WriteErrorLine(e.Message);
        }
    }
    
    private void ArchiveFiles(string source, string destination, string? configName)
    {
        try
        {
            var isValid = TryGetConfigAndSourceFolders(source, destination, configName, out var config, out var sourceFolders);
            if (!isValid || config == null)
                return;
            
            Write("Archiving ");
            PrintFolderList(sourceFolders);
            WriteLine();

            var lastFolder = string.Empty;
            var folderArchiver = new MultiFolderArchiver(sourceFolders, config);
            foreach (var fileArchiveResult in folderArchiver.ArchiveTo(destination))
            {
                if (lastFolder != fileArchiveResult.Folder)
                {
                    WriteLine();
                    WriteLine(HeaderColor, $"Archiving files from '{fileArchiveResult.Folder}' to '{destination}'");
                    lastFolder = fileArchiveResult.Folder;
                }
                
                PrintFileResults(fileArchiveResult.FileResult, folderArchiver.Result);
            }
            
            WriteLine();
            PrintResults(folderArchiver.Result);
        }
        catch (Exception e)
        {
            WriteErrorLine(e.Message);
        }
    }

    private bool TryGetConfigAndSourceFolders(
        string source, 
        string destination, 
        string? configName, 
        out ArchiveConfig? config,
        out IReadOnlyCollection<string> sourceFolders)
    {
        if (string.IsNullOrEmpty(configName))
        {
            WriteErrorLine($"ERROR: Config not specified.");
            sourceFolders = [];
            config = null;
            return false;
        }

        config = ArchiveConfig.Load(configName);
        if (config == null)
        {
            WriteErrorLine($"ERROR: Config '{configName}' not found.");
            sourceFolders = [];
            config = null;
            return false;
        }

        if (source == "CAMERAS")
        {
            if (FolderUtils.GetAllConnectedCameraFolders() is { Count: > 0 } folders)
            {
                sourceFolders = folders;
            }
            else
            {
                WriteErrorLine("ERROR: No Camera folders found.");
                sourceFolders = [];
                return false;
            }
        }
        else
        {
            sourceFolders = [source];
        }
            
        if (ArchiveConfig.Load(Path.Combine(destination, "archive-config.json")) is { } destConfig)
            config = config.Merge(destConfig);
        
        return true;
    }

    private void PrintFolderList(IReadOnlyCollection<string> sourceFolders, string? currentFolder = null)
    {
        Write("Folders: [");
        var color = string.IsNullOrEmpty(currentFolder) ? ConsoleColor.Gray : ConsoleColor.Green;
        var currentFound = false;
        foreach (var folder in sourceFolders)
        {
            if (currentFound)
                color = ConsoleColor.Gray;
            
            Write(color,$"'{folder}'");
            if (folder != sourceFolders.Last())
                Write(", ");
            
            if (folder == currentFolder)
                currentFound = true;
        }
        
        WriteLine("]");
    }

    private void PrintResults(IArchiverResult result)
    {
        var isScanResult = result is ScannedArchiverResults;
        
        Write($"Valid Files: ");
        Write(ConsoleColor.Cyan, $"{result.TotalFilesCount - result.InvalidFileCount}");
        Write(" of ");
        WriteLine(ConsoleColor.Cyan, $"{result.TotalFilesCount}");
        if (result.TotalFilesCount - result.InvalidFileCount > 0)
        {
            if (result.CopiedFileCount > 0)
            {
                Write(isScanResult ? "To copy: " : "Copied: ");
                WriteLine(ConsoleColor.Green, $"{result.CopiedFileCount}");
            }

            if (result.MovedFilesCount > 0)
            {
                Write(isScanResult ? "To Move: " : "Moved: ");
                WriteLine(ConsoleColor.Green, $"{result.MovedFilesCount}");
            }

            if (result.UpdatedFilesCount > 0)
            {
                Write(isScanResult ? "To Update: " : "Updated: ");
                WriteLine(ConsoleColor.Yellow, $"{result.UpdatedFilesCount}");
            }
            
            if (result.SrcDeletedFilesCount > 0)
            {
                Write(isScanResult ? "To Delete [SRC]: " : "Deleted [SRC]: ");
                WriteLine(ConsoleColor.Red, $"{result.SrcDeletedFilesCount} ({result.SrcDeletedBytes.ToHumanReadableByteSize()})");
            }
            
            if (result.DestDeletedFilesCount > 0)
            {
                Write(isScanResult ? "To Delete [DST]: " :"Deleted [DST]: ");
                WriteLine(ConsoleColor.Red, $"{result.DestDeletedFilesCount} ({result.DestDeletedBytes.ToHumanReadableByteSize()})");
            }

            if (result.DuplicatedFileCount > 0)
                WriteLine($"Duplicates: {result.DuplicatedFileCount}");
            
            if (result.TransferredBytes > 0)
                WriteLine($"Data {(isScanResult ? "to Transfer" : "Transferred")} {result.TransferredBytes.ToSizeString()}");

            if (result.FailedFilesCount > 0)
                WriteLine(ConsoleColor.Red, $"Failed: {result.FailedFilesCount}");
        }
        else
        {
            WriteLine(ConsoleColor.Yellow, "Nothing to archive.");
        }

        if (result.ElapsedTime.TotalSeconds > 1)
        {
            WriteLine($"Elapsed time: {result.ElapsedTime.ToHumanReadableString()}");
        }
    }
    
    private void PrintFileResults(FileArchiveResult result, IProgressArchiverResult progress)
    {
        PrintResultAction(result.Result, progress);

        if (result.Context.DryRun && result.Result != FileResult.Invalid)
        {
            Write(ConsoleColor.Red, "[DRY] ");
        }
        
        switch (result.Result)
        {
            case FileResult.Copied:
            case FileResult.Moved:
            case FileResult.CopiedUpdated:
            case FileResult.MovedUpdated:
                WriteLine($"'{result.Context.SourceFileFullPath}' -> '{result.Context.DestFileFullPath}'");
                break;
            case FileResult.SourceFileDeleted:
                WriteLine($"'{result.Context.SourceFileFullPath}'");
                break;
            case FileResult.DestFileDeleted:
                WriteLine(result.Message!);
                break;
            case FileResult.Error:
                WriteErrorLine($"ERROR: '{result.Exception?.Message ?? result.Message ?? "Unknown Error"}'");
                break;
            case FileResult.AlreadyExists:
                if (string.IsNullOrEmpty(result.Message))
                    WriteLine($"'{result.Context.SourceFileFullPath}' in '{result.Context.DestinationFolderPath}'");
                else    
                    WriteLine($"'{result.Context.SourceFileFullPath}' as '{result.Message}'");
                break;
            case FileResult.Invalid:
                break;
        }
    }

    private void PrintResultAction(FileResult result, IProgressArchiverResult progress)
    {
        switch (result)
        {
            case FileResult.Copied:
                WriteProgress();
                Write(ConsoleColor.Green, "COPIED: ");
                break;
            case FileResult.Moved:
                WriteProgress();
                Write(ConsoleColor.Green, "MOVED: ");
                break;
            case FileResult.CopiedUpdated:
                WriteProgress();
                Write(ConsoleColor.Green, "COPIED ");
                Write(ConsoleColor.Yellow, "[DEST UPDATED]: ");
                break;
            case FileResult.MovedUpdated:
                WriteProgress();
                Write(ConsoleColor.Green, "MOVED ");
                Write(ConsoleColor.Yellow, "[DEST UPDATED]: ");
                break;
            case FileResult.SourceFileDeleted:
            case FileResult.DestFileDeleted:
                WriteProgress();
                Write(ConsoleColor.Yellow, "DELETED: ");
                break;
            case FileResult.AlreadyExists: // Already exists in dest with another name.
                WriteProgress();
                Write("DUPLICATED: ");
                break;
            case FileResult.Invalid: // Does not meet filter or metadata criteria.
                break;
        }

        void WriteProgress()
        {
            var percentage = progress.Percentage;
            if (percentage is > 0 and <= 1)
            {
                Write(ConsoleColor.Cyan, $"[{progress.ValidFileCount}/{progress.ValidScannedFileCount} ({percentage:P1}) ({progress.RemainingTime?.ToHumanReadableString(true)})] ");
            }
        }
    }
}