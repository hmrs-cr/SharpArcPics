using System.Collections;
using System.Diagnostics;
using PicArchiver.Core.Configs;
using PicArchiver.Core.Metadata.Loaders;

namespace PicArchiver.Core;

public interface IFolderArchiverResult
{
    int FailedFilesCount { get; }
    int DeletedFilesCount { get; }
    int UpdatedFilesCount { get; }
    int MovedFilesCount { get; }
    int DuplicatedFileCount { get; }
    int InvalidFileCount { get; }
    int CopiedFileCount { get; }
    int ProcessedFilesCount { get; }
    int TotalFilesCount { get; }
    TimeSpan ElapsedTime { get; }

    long TransferredBytes { get; }
}

public class AggregatedFolderArchiverResult : IFolderArchiverResult, IEnumerable<IFolderArchiverResult>
{
    private readonly ICollection<IFolderArchiverResult> _results;
    private readonly long _startTime;
    private readonly int _maxCount;

    public int FailedFilesCount { get; private set; }
    public int DeletedFilesCount { get; private set; }
    public int UpdatedFilesCount { get; private set; }
    public int MovedFilesCount { get; private set; }
    public int DuplicatedFileCount { get; private set; }
    public int InvalidFileCount { get; private set; }
    public int CopiedFileCount { get; private set; }
    public int ProcessedFilesCount { get; private set; }
    public int TotalFilesCount { get; private set; }
    public TimeSpan ElapsedTime { get; private set; }
    
    public long TransferredBytes { get; private set; }
    
    public int Count => _results.Count;

    public AggregatedFolderArchiverResult(int maxCount)
    {
        _maxCount = maxCount;
        _results = new List<IFolderArchiverResult>(_maxCount);
        _startTime = Stopwatch.GetTimestamp();
    }

    public void Add(IFolderArchiverResult folderArchiverResult)
    {
        if (Count == _maxCount)
            throw new InvalidOperationException("Folder archiver has already been archived");
            
        FailedFilesCount += folderArchiverResult.FailedFilesCount;
        DeletedFilesCount += folderArchiverResult.DeletedFilesCount;
        UpdatedFilesCount += folderArchiverResult.UpdatedFilesCount;
        MovedFilesCount += folderArchiverResult.MovedFilesCount;
        DuplicatedFileCount += folderArchiverResult.DuplicatedFileCount;
        InvalidFileCount += folderArchiverResult.InvalidFileCount;
        CopiedFileCount += folderArchiverResult.CopiedFileCount;
        ProcessedFilesCount += folderArchiverResult.ProcessedFilesCount;
        TotalFilesCount += folderArchiverResult.TotalFilesCount;
        TransferredBytes += folderArchiverResult.TransferredBytes;
        
        ElapsedTime = Stopwatch.GetElapsedTime(_startTime);

        _results.Add(folderArchiverResult);
    }

    public IEnumerator<IFolderArchiverResult> GetEnumerator() => _results.GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class FolderArchiver : IDisposable, IFolderArchiverResult
{
    public int FailedFilesCount { get; private set; }

    public int DeletedFilesCount { get; private set; }

    public int UpdatedFilesCount { get; private set; }

    public int MovedFilesCount { get; private set; }
    
    public int DuplicatedFileCount { get; private set; }

    public int InvalidFileCount { get; private set; }

    public int CopiedFileCount { get; private set; }
    
    public int ProcessedFilesCount => MovedFilesCount + UpdatedFilesCount + DeletedFilesCount + CopiedFileCount;
    
    public int TotalFilesCount { get; private set; }
    
    public TimeSpan ElapsedTime { get; private set; }
    
    public long TransferredBytes { get; private set; }

    public string SourceFolder { get; }
    
    public ArchiveConfig Config { get; }

    public FolderArchiver(string sourceFolder, ArchiveConfig config)
    {
        SourceFolder = sourceFolder;
        Config = config;
    }
    
    public IEnumerable<FileArchiveContext> ArchiveTo(string destFolder)
    {
        var ts = Stopwatch.GetTimestamp();
        var files = Directory.GetFiles(SourceFolder, "*", SearchOption.AllDirectories);
        TotalFilesCount = files.Length;
        
        Directory.CreateDirectory(destFolder);
        foreach (var file in files)
        {
            yield return InternalProcessFile(file, destFolder);
        }
        
        ElapsedTime = Stopwatch.GetElapsedTime(ts);
    }
    
    private FileArchiveContext InternalProcessFile(string sourceFileName, string destDirectory)
    {
        var result = ProcessFile(sourceFileName, destDirectory);
        
        switch (result.Result)
        {
            case FileResult.Moved:
                MovedFilesCount++;
                break;
            case FileResult.CopiedUpdated:
            case FileResult.MovedUpdated:
                UpdatedFilesCount++;
                break;  
            case FileResult.SourceFileDeleted:
                DeletedFilesCount++;
                break;
            case FileResult.Error:
                FailedFilesCount++;
                break;
            case FileResult.Copied:
                CopiedFileCount++;
                break;
            case FileResult.AlreadyExists:
                DuplicatedFileCount++;
                break;
            case FileResult.Invalid:
                InvalidFileCount++;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        return result;
    }

    private FileArchiveContext ProcessFile(string sourceFileName, string destDirectory)
    {
        using var context = new FileArchiveContext(sourceFileName, destDirectory, Config);
        if (!context.IsValid)
        {
            if (context.Metadata.GetExistingFileName() is { } existingPicName)
            {
                return context.SetResult(FileResult.AlreadyExists, message: existingPicName);
            }
            
            return context.SetResult(FileResult.Invalid);
        }

        try
        {
            var updated = false;
            var canOverwrite = false;
            var destExists = context.DestinationFileExists;
            if (destExists)
            {
                if (context.DeleteSourceFileIfDestExists)
                {
                    if (!context.DryRun)
                        File.Delete(sourceFileName);
                    
                    return context.SetResult(FileResult.SourceFileDeleted);
                }

                canOverwrite = context.OverrideDestinationFile;
                if (canOverwrite)
                {
                    if (!context.DryRun)
                        File.Delete(context.DestFileFullPath);
                    
                    updated = true;
                }
            }

            if (destExists && !canOverwrite)
            {
                return context.SetResult(FileResult.AlreadyExists);
            }
            
            if (context.MoveSourceFile)
            {
                if (!context.DryRun)
                {
                    var sourceFileLength = context.SourceFileInfo.Length;
                    File.Move(sourceFileName, context.DestFileFullPath, overwrite: canOverwrite);
                    TransferredBytes += sourceFileLength;
                }
                
                return context.SetResult(updated ? FileResult.MovedUpdated : FileResult.Moved);
            }
            
            if (!context.DryRun)
            {
                File.Copy(sourceFileName, context.DestFileFullPath, overwrite: canOverwrite);
                TransferredBytes += context.SourceFileInfo.Length;
            }
            
            return context.SetResult(updated ? FileResult.CopiedUpdated : FileResult.Copied);
        }
        catch (Exception e)
        {
            return context.SetResult(FileResult.Error, exception: e);
        }
    }

    public void Dispose() => Config.MetadataLoaderInstances.Finalize(null);
}