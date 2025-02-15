using System.CommandLine;
using PicArchiver.Core;
using PicArchiver.Core.Configs;
using PicArchiver.Extensions;

namespace PicArchiver.Commands;

public class ArchiverCommand : BaseCommand 
{
    const ConsoleColor HeaderColor = ConsoleColor.Cyan; 
        
    internal ArchiverCommand() : base("archive", "Archives files from source to destination applying logic defined by config.")
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
        
        this.SetHandler(ArchiveFiles, sourceOption, destinationOption, configNameOption);
    }
    
    public void ArchiveFiles(string source, string destination, string? configName)
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

            var results = new AggregatedFolderArchiverResult(sourceFolders.Count);
            foreach (var sourceFolder in sourceFolders)
            {
                if (results.Count > 0)
                    WriteLine(string.Empty);
                
                WriteLine(HeaderColor, $"Archiving files from '{sourceFolder}' to '{destination}'...");
                var result = ArchiveFilesInternal(sourceFolder, destination, config);

                if (sourceFolders.Count > 1)
                {
                    WriteLine(string.Empty);
                    WriteLine(HeaderColor, $"Results for '{sourceFolder}':");
                    PrintResuls(result);
                    results.Add(result);
                    
                    WriteLine(string.Empty);
                    WriteLine(HeaderColor, "Global results:");
                    PrintFolderList(sourceFolders, sourceFolder);
                    PrintResuls(results);
                    WriteLine(string.Empty);
                }
                else
                {
                    WriteLine(string.Empty);
                    PrintResuls(result);
                    WriteLine(string.Empty);
                }
            } 
            
        }
        catch (Exception e)
        {
            WriteErrorLine(e.Message);
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

    private IFolderArchiverResult ArchiveFilesInternal(string sourceDirectory, string destinationDirectory, ArchiveConfig config)
    {
        using var archiver = new FolderArchiver(sourceDirectory, config);
        
        var fileResults = archiver.ArchiveTo(destinationDirectory);
        foreach (var result in fileResults)
        {
            PrintResuls(result);
        }

        IFolderArchiverResult archiveResult = archiver;
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
                Write("Copied: ");
                WriteLine(ConsoleColor.Green, $"{result.CopiedFileCount}");
            }

            if (result.MovedFilesCount > 0)
            {
                Write("Moved: ");
                WriteLine(ConsoleColor.Green, $"{result.MovedFilesCount}");
            }

            if (result.UpdatedFilesCount > 0)
            {
                Write("Updated: ");
                WriteLine(ConsoleColor.Yellow, $"{result.UpdatedFilesCount}");
            }
            
            if (result.SrcDeletedFilesCount > 0)
            {
                Write("Deleted [SRC]: ");
                WriteLine(ConsoleColor.Red, $"{result.SrcDeletedFilesCount} ({result.SrcDeleteBytes.ToHumanReadableByteSize()})");
            }
            
            if (result.DestDeletedFilesCount > 0)
            {
                Write("Deleted [DST]: ");
                WriteLine(ConsoleColor.Red, $"{result.DestDeletedFilesCount} ({result.DestDeleteBytes.ToHumanReadableByteSize()})");
            }

            if (result.DuplicatedFileCount > 0)
                WriteLine($"Duplicates: {result.DuplicatedFileCount}");
            
            if (result.TransferredBytes > 0)
                WriteLine($"Data Transferred: {result.TransferredBytes.ToSizeString()}");

            if (result.FailedFilesCount > 0)
                WriteLine(ConsoleColor.Red, $"Failed: {result.FailedFilesCount}");
        }
        else
        {
            WriteLine(ConsoleColor.Yellow, "Nothing to archive.");
        }


        WriteLine($"Elapsed time: {result.ElapsedTime.ToHumanReadableString()}");
    }
    
    private void PrintResuls(FileArchiveResult result)
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
                Write(ConsoleColor.Green, "COPIED: ");
                break;
            case FileResult.Moved:
                Write(ConsoleColor.Green, "MOVED: ");
                break;
            case FileResult.CopiedUpdated:
                Write(ConsoleColor.Green, "COPIED ");
                Write(ConsoleColor.Yellow, "[DEST UPDATED]: ");
                break;
            case FileResult.MovedUpdated:
                Write(ConsoleColor.Green, "MOVED ");
                Write(ConsoleColor.Yellow, "[DEST UPDATED]: ");
                break;
            case FileResult.SourceFileDeleted:
            case FileResult.DestFileDeleted:
                Write(ConsoleColor.Yellow, "DELETED: ");
                break;
            case FileResult.AlreadyExists: // Already exists in dest with another name.
                Write("DUPLICATED: ");
                break;
            case FileResult.Invalid: // Does not meet filter or metadata criteria.
                break;
        }
    }
}