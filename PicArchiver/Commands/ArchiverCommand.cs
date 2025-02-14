using System.CommandLine;
using PicArchiver.Core;
using PicArchiver.Core.Configs;
using PicArchiver.Extensions;

namespace PicArchiver.Commands;

public class ArchiverCommand : BaseCommand 
{
    const ConsoleColor HeaderColor = ConsoleColor.Cyan;

    private readonly bool _scanOnly = false;
    private IFolderArchiverResult? _scanResults;
    private IFolderArchiverResult? _globalResults;

    internal ArchiverCommand(bool scanOnly = false) : base(
        name: scanOnly ? "scan" :
                         "archive", 
        
        description: scanOnly ? "Scans source folder for possible changes according to destination folder and config" : 
                                "Archives files from source to destination applying logic defined by config.")
    {
        _scanOnly = scanOnly;
        
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
        
        this.SetHandler(ArchiveFiles, sourceOption, destinationOption, configNameOption);
    }
    
    private void ArchiveFiles(string source, string destination, string? configName)
    {
        try
        {
            if (string.IsNullOrEmpty(configName))
            {
                WriteErrorLine($"ERROR: Config not specified.");
                return;
            }

            var config = ArchiveConfig.Load(configName);
            if (config == null)
            {
                WriteErrorLine($"ERROR: Config '{configName}' not found.");
                return;
            }

            ICollection<string> sourceFolders;
            if (source == "CAMERAS")
            {
                if (FolderUtils.GetAllConnectedCameraFolders() is { Count: > 0 } folders)
                {
                    sourceFolders = folders;
                }
                else
                {
                    WriteErrorLine("ERROR: No Camera folders found.");
                    return;
                }
                
                Write("Detected Source ");
                PrintFolderList(sourceFolders);
                
                WriteLine(string.Empty);
            }
            else
            {
                sourceFolders = [source];
            }
            
            if (ArchiveConfig.Load(Path.Combine(destination, "archive-config.json")) is { } destConfig)
                config = config.Merge(destConfig);
            
            if (_scanOnly || config.ReportProgress == true)
                _scanResults = ArchiveOrScanFiles(destination, sourceFolders, config, true);
            
            if (_scanOnly)
            {
                UnbreakLine();
                PrintResuls(_scanResults);
            }
            else
            {
                UnbreakLine();
                ArchiveOrScanFiles(destination, sourceFolders, config, scanOnly: false);
            }
        }
        catch (Exception e)
        {
            WriteErrorLine(e.Message);
        }
    }

    private IFolderArchiverResult ArchiveOrScanFiles(
        string destination, 
        ICollection<string> sourceFolders, 
        ArchiveConfig config, 
        bool scanOnly)
    {
         var globalResults = new AggregatedFolderArchiverResult(sourceFolders.Count);
         _globalResults = globalResults;
         
        foreach (var sourceFolder in sourceFolders)
        {
            if (globalResults.Count > 0)
                WriteLine(string.Empty);
                
            if (!scanOnly)
                WriteLine(HeaderColor, $"Archiving files from '{sourceFolder}' to '{destination}'...");
            var result = ArchiveFilesInternal(sourceFolder, destination, config, scanOnly);
            globalResults.Add(result);
            
            if (!scanOnly)
                PrintResults(sourceFolders, sourceFolder, result, globalResults);
        }
        
        return _globalResults;
    }

    private void PrintResults(ICollection<string> sourceFolders, string sourceFolder, IFolderArchiverResult result,
        AggregatedFolderArchiverResult globalResults)
    {
        if (sourceFolders.Count > 1)
        {
            WriteLine(string.Empty);
            WriteLine(HeaderColor, $"Results for '{sourceFolder}':");
            PrintResuls(result);
                    
            WriteLine(string.Empty);
            WriteLine(HeaderColor, "Global results:");
            PrintFolderList(sourceFolders, sourceFolder);
            PrintResuls(globalResults);
            WriteLine(string.Empty);
        }
        else
        {
            WriteLine(string.Empty);
            PrintResuls(result);
            WriteLine(string.Empty);
        }
    }

    private void PrintFolderList(ICollection<string> sourceFolders, string? currentFolder = null)
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

    private IFolderArchiverResult ArchiveFilesInternal(
        string sourceDirectory,
        string destinationDirectory,
        ArchiveConfig config, 
        bool scanOnly)
    {
        using var archiver = new FolderArchiver(sourceDirectory, config);
        
        var fileResults = archiver.ArchiveTo(destinationDirectory, scanOnly);
        IFolderArchiverResult archiveResult = archiver;
        
        foreach (var result in fileResults)
        {
            if (scanOnly)
            {
                WriteSpinner("Scanning...");
            }
            else
            {
                PrintFileResuls(result);
            }
        }
        return archiveResult;
    }

    private void PrintResuls(IFolderArchiverResult result)
    {
        Write($"Valid Files: ");
        Write(ConsoleColor.Cyan, $"{result.TotalFilesCount - result.InvalidFileCount}");
        Write(" of ");
        WriteLine(ConsoleColor.Cyan, $"{result.TotalFilesCount}");
        if (result.TotalFilesCount - result.InvalidFileCount > 0)
        {
            if (result.CopiedFileCount > 0)
            {
                Write(_scanOnly ? "To copy: " : "Copied: ");
                WriteLine(ConsoleColor.Green, $"{result.CopiedFileCount}");
            }

            if (result.MovedFilesCount > 0)
            {
                Write(_scanOnly ? "To Move: " : "Moved: ");
                WriteLine(ConsoleColor.Green, $"{result.MovedFilesCount}");
            }

            if (result.UpdatedFilesCount > 0)
            {
                Write(_scanOnly ? "To Update: " : "Updated: ");
                WriteLine(ConsoleColor.Yellow, $"{result.UpdatedFilesCount}");
            }
            
            if (result.SrcDeletedFilesCount > 0)
            {
                Write(_scanOnly ? "To Delete [SRC]: " : "Deleted [SRC]: ");
                WriteLine(ConsoleColor.Red, $"{result.SrcDeletedFilesCount} ({result.SrcDeleteBytes.ToHumanReadableByteSize()})");
            }
            
            if (result.DestDeletedFilesCount > 0)
            {
                Write(_scanOnly ? "To Delete [DST]: " :"Deleted [DST]: ");
                WriteLine(ConsoleColor.Red, $"{result.DestDeletedFilesCount} ({result.DestDeleteBytes.ToHumanReadableByteSize()})");
            }

            if (result.DuplicatedFileCount > 0)
                WriteLine($"Duplicates: {result.DuplicatedFileCount}");
            
            if (result.TransferredBytes > 0)
                WriteLine($"Data {(_scanOnly ? "to Transfer" : "Transferred")} {result.TransferredBytes.ToSizeString()}");

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
    
    private void PrintFileResuls(FileArchiveResult result)
    {
        PrintResultAction(result.Result);

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

    private void PrintResultAction(FileResult result)
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
            if (_scanResults != null && _globalResults != null)
            {
                var totalValidFileCount = _scanResults.TotalFilesCount - _scanResults.InvalidFileCount;
                var currentFileCount = _globalResults.TotalFilesCount - _globalResults.InvalidFileCount;
                var percent = (double)currentFileCount / totalValidFileCount;
                Write(ConsoleColor.Cyan, $"[{currentFileCount}/{totalValidFileCount} ({percent:P1})] ");
            }
        }
    }
}