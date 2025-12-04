using System.Collections;
using System.Diagnostics;
using PicArchiver.Core.Configs;
using PicArchiver.Core.Metadata.Loaders;
using PicArchiver.Extensions;

namespace PicArchiver.Core;

public interface IArchiverResult
{
    int FailedFilesCount { get; }
    int SrcDeletedFilesCount { get; }
    int DestDeletedFilesCount { get; } 
    long SrcDeletedBytes { get; }
    long DestDeletedBytes { get; }
    int UpdatedFilesCount { get; }
    int MovedFilesCount { get; }
    int DuplicatedFileCount { get; }
    int InvalidFileCount { get; }
    int CopiedFileCount { get; }
    int ProcessedFilesCount { get; }
    int TotalFilesCount { get; }
    TimeSpan ElapsedTime { get; }

    long TransferredBytes { get; }
    long ProcessedBytes { get; }
}

public interface IFolderArchiverResult : IArchiverResult
{
    string SourceFolderPath { get; }
}

public interface IAggregatedArchiverResult : IArchiverResult, IReadOnlyCollection<IArchiverResult>;

public interface IProgressArchiverResult : IArchiverResult
{
    int ValidFileCount { get; }
    int? ValidScannedFileCount { get; }
    TimeSpan? RemainingTime { get; }
    double? Percentage { get; }
}

public class ScannedArchiverResults(int maxCount) : AggregatedArchiverResult(maxCount);
public class AggregatedArchiverResult : IAggregatedArchiverResult
{
    private readonly ICollection<IArchiverResult> _results;
    private readonly long _startTime;
    private readonly int _maxCount;

    public int FailedFilesCount { get; private set; }
    public int SrcDeletedFilesCount { get; private set; }
    
    public int DestDeletedFilesCount { get; private set; }
    public long SrcDeletedBytes { get; private set; }
    public long DestDeletedBytes { get; private set; }
    public int UpdatedFilesCount { get; private set; }
    public int MovedFilesCount { get; private set; }
    public int DuplicatedFileCount { get; private set; }
    public int InvalidFileCount { get; private set; }
    public int CopiedFileCount { get; private set; }
    public int ProcessedFilesCount { get; private set; }
    public int TotalFilesCount { get; private set; }
    public TimeSpan ElapsedTime { get; private set; }
    
    public long TransferredBytes { get; private set; }
    public long ProcessedBytes { get; private set; }

    public int Count => _results.Count;

    public AggregatedArchiverResult(int maxCount)
    {
        _maxCount = maxCount;
        _results = new List<IArchiverResult>(_maxCount);
        _startTime = Stopwatch.GetTimestamp();
    }
    
    public AggregatedArchiverResult(IReadOnlyCollection<IFolderArchiverResult> results)
    {
        _maxCount = results.Count;
        _results = new List<IArchiverResult>(_maxCount);
        _startTime = Stopwatch.GetTimestamp();
        foreach (var folderArchiverResult in results)
        {
            Add(folderArchiverResult);
        }
    }

    public void Add(IArchiverResult folderArchiverResult)
    {
        if (Count == _maxCount)
            throw new InvalidOperationException("Folder archiver has already been archived");
            
        FailedFilesCount += folderArchiverResult.FailedFilesCount;
        SrcDeletedFilesCount += folderArchiverResult.SrcDeletedFilesCount;
        UpdatedFilesCount += folderArchiverResult.UpdatedFilesCount;
        MovedFilesCount += folderArchiverResult.MovedFilesCount;
        DuplicatedFileCount += folderArchiverResult.DuplicatedFileCount;
        InvalidFileCount += folderArchiverResult.InvalidFileCount;
        CopiedFileCount += folderArchiverResult.CopiedFileCount;
        ProcessedFilesCount += folderArchiverResult.ProcessedFilesCount;
        TotalFilesCount += folderArchiverResult.TotalFilesCount;
        TransferredBytes += folderArchiverResult.TransferredBytes;
        ProcessedBytes += folderArchiverResult.ProcessedBytes;
        DestDeletedFilesCount += folderArchiverResult.DestDeletedFilesCount;
        SrcDeletedBytes += folderArchiverResult.SrcDeletedBytes;
        DestDeletedBytes += folderArchiverResult.DestDeletedBytes;
        
        ElapsedTime = Stopwatch.GetElapsedTime(_startTime);

        _results.Add(folderArchiverResult);
    }

    public IEnumerator<IArchiverResult> GetEnumerator() => _results.GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class ProgressArchiverResult : IProgressArchiverResult
{
    public static readonly IProgressArchiverResult Default = new ProgressArchiverResult();
    
    private readonly long _startTime;
    private readonly Dictionary<string, IFolderArchiverResult>? _results;
    private IArchiverResult? _aggregatedResults;
    private readonly IAggregatedArchiverResult? _scanResults;

    public ProgressArchiverResult(IAggregatedArchiverResult? scanResults)
    {
        _scanResults = scanResults;
        _results = new Dictionary<string, IFolderArchiverResult>(scanResults?.Count ?? 2);
        _startTime = Stopwatch.GetTimestamp();
    }

    private ProgressArchiverResult() { }
    
    public int FailedFilesCount => _aggregatedResults?.FailedFilesCount ?? 0;
    public int SrcDeletedFilesCount => _aggregatedResults?.SrcDeletedFilesCount ?? 0;
    public int DestDeletedFilesCount => _aggregatedResults?.DestDeletedFilesCount ?? 0;
    public long SrcDeletedBytes => _aggregatedResults?.SrcDeletedBytes ?? 0;
    public long DestDeletedBytes => _aggregatedResults?.DestDeletedBytes ?? 0;
    public int UpdatedFilesCount => _aggregatedResults?.UpdatedFilesCount ?? 0;
    public int MovedFilesCount => _aggregatedResults?.MovedFilesCount ?? 0;
    public int DuplicatedFileCount => _aggregatedResults?.DuplicatedFileCount ?? 0;
    public int InvalidFileCount => _aggregatedResults?.InvalidFileCount ?? 0;
    public int CopiedFileCount => _aggregatedResults?.CopiedFileCount ?? 0;
    public int ProcessedFilesCount => _aggregatedResults?.ProcessedFilesCount ?? 0;
    public int TotalFilesCount => _aggregatedResults?.TotalFilesCount ?? 0;
    public TimeSpan ElapsedTime => Stopwatch.GetElapsedTime(_startTime);
    public long TransferredBytes => _aggregatedResults?.TransferredBytes ?? 0;
    public long ProcessedBytes => _aggregatedResults?.ProcessedBytes ?? 0;
    
    public int ValidFileCount => TotalFilesCount - InvalidFileCount;
    public int? ValidScannedFileCount => _scanResults?.TotalFilesCount - _scanResults?.InvalidFileCount;

    public TimeSpan? RemainingTime
    {
        get
        {
            if (_scanResults == null)
                return null;

            var bytesPerSecond = ProcessedBytes / ElapsedTime.TotalSeconds;
            var remainingBytes = _scanResults.ProcessedBytes - ProcessedBytes;
            var remainingSeconds = remainingBytes / bytesPerSecond;

            return TimeSpan.FromSeconds(remainingSeconds);
        }
    }
    public double? Percentage => _scanResults != null ? (double)ProcessedBytes / _scanResults.ProcessedBytes : null;

    public void UpdateProgress(IFolderArchiverResult folderArchiverResult)
    {
        if (_results == null)
            throw new InvalidOperationException("Default instance does not allow Updates");
        
        _results[folderArchiverResult.SourceFolderPath] = folderArchiverResult;
        _aggregatedResults = new AggregatedArchiverResult(_results.Values);
    }
}

public class FolderArchiver : IDisposable, IFolderArchiverResult
{
    private const string TotalDestDeleteSizeKey = "TotalDestDeletedSize";
    
    public int FailedFilesCount { get; private set; }

    public int SrcDeletedFilesCount { get; private set; }
    public int DestDeletedFilesCount { get; private set; }
    public int SkippedFilesCount { get; private set; }
    
    public int UpdatedFilesCount { get; private set; }

    public int MovedFilesCount { get; private set; }
    
    public int DuplicatedFileCount { get; private set; }

    public int InvalidFileCount { get; private set; }

    public int CopiedFileCount { get; private set; }
    
    public int ProcessedFilesCount => MovedFilesCount + UpdatedFilesCount + SrcDeletedFilesCount + CopiedFileCount;
    
    public int TotalFilesCount { get; private set; }
    
    public TimeSpan ElapsedTime { get; private set; }
    
    public long TransferredBytes { get; private set; }
    public long ProcessedBytes { get; private set; }
    public long SrcDeletedBytes { get; private set; }
    public long DestDeletedBytes { get; private set; }

    public string SourceFolderPath { get; }
    
    public ArchiveConfig Config { get; }
    
    private static readonly EnumerationOptions EnumerationOptionsTopLevelOnly = new() { RecurseSubdirectories = false, IgnoreInaccessible = true };
    private static readonly EnumerationOptions EnumerationOptionsRecursive = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

    public FolderArchiver(string sourceFolder, ArchiveConfig config)
    {
        SourceFolderPath = sourceFolder;
        Config = config;
    }

    public IEnumerable<FileArchiveResult> ArchiveTo(string destFolder) => ArchiveToInternal(destFolder, false);

    public IEnumerable<FileArchiveResult> ScanSrcFiles(string? destFolder = null) => ArchiveToInternal(destFolder ?? "/dev/null", true);
    
    public IEnumerable<FileArchiveResult> ArchiveToInternal(string destFolder, bool scanOnly)
    {
        var ts = Stopwatch.GetTimestamp();
        ResetCounters();
        
        var files = EnumerateFiles(SourceFolderPath, Config.Recursive == true);
        foreach (var file in files)
        {
            var results = ProcessFile(file, destFolder, scanOnly);
            foreach (var result in results)
            {
                UpdateCounters(result.Result);
                yield return result;
            }
        }
        
        ElapsedTime = Stopwatch.GetElapsedTime(ts);
    }

    public static IEnumerable<string> EnumerateFiles(string folderPath, bool recurseSubdirectories)
    {
        if (File.Exists(Path.Join(folderPath, "no_archive")))
            yield break;

        var files = Directory.GetFiles(folderPath, "*", EnumerationOptionsTopLevelOnly).OrderBy(f => f);
        foreach (var file in files)
            yield return file;

        if (!recurseSubdirectories)
            yield break;

        var directories = Directory.GetDirectories(folderPath, "*", EnumerationOptionsTopLevelOnly).OrderBy(d => d);
        foreach (var directory in directories)
            foreach (var file in EnumerateFiles(directory, recurseSubdirectories))
                yield return file;
    }

    private void ResetCounters()
    {
        MovedFilesCount = 0;
        UpdatedFilesCount = 0;
        SrcDeletedFilesCount = 0;
        FailedFilesCount = 0;
        SkippedFilesCount = 0;
        CopiedFileCount = 0;
        DuplicatedFileCount = 0;
        InvalidFileCount = 0;
        DestDeletedFilesCount = 0;
        TotalFilesCount = 0;
    }

    private void UpdateCounters(FileResult result)
    {
        switch (result)
        {
            case FileResult.Moved:
                MovedFilesCount++;
                break;
            case FileResult.CopiedUpdated:
            case FileResult.MovedUpdated:
                UpdatedFilesCount++;
                break;
            case FileResult.SourceFileDeleted:
                SrcDeletedFilesCount++;
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
            case FileResult.DestFileDeleted:
                DestDeletedFilesCount++;
                break;
            case FileResult.Skipped:
                SkippedFilesCount++;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (result != FileResult.DestFileDeleted)
        {
            TotalFilesCount++;
        }
    }

    private IEnumerable<FileArchiveResult> ProcessFile(string sourceFileName, string destDirectory, bool scanOnly = false)
    {
        var config = Config;
        if (scanOnly)
            config = config.Merge(new ArchiveConfig { DryRun = true });
        
        using var context = new FileArchiveContext(sourceFileName, destDirectory, config);

        var countBytes = !context.DryRun || scanOnly;
        if (context.IsValid)
        {
            ProcessedBytes += context.SourceFileInfo.Length;
            
            var updated = false;
            var canOverwrite = false;
            var destExists = context.DestinationFileExists;
            if (destExists)
            {
                if (context.DeleteSourceFileIfDestExists)
                {
                    var sourceFileLength = context.SourceFileInfo.Length;
                    if (!context.DryRun)
                    {
                        File.Delete(sourceFileName);
                    }
                    
                    if (countBytes)
                        SrcDeletedBytes += sourceFileLength;

                    yield return context.CreateResult(FileResult.SourceFileDeleted);
                    yield break;
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
                yield return context.CreateResult(FileResult.AlreadyExists);
                yield break;
            }

            if (context.SkipNewFiles)
            {
                yield return context.CreateResult(FileResult.Skipped);
                yield break;
            }
            
            if (context.Config.Rotate == true || context.Config.MinDriveSize.HasValue)
            {
                var requiredSize = context.Config.MinDriveSize ?? context.SourceFileInfo.Length;
                var deleteResults = EnsureDestDriveHasEnoughFreeSpace(context, requiredSize);

                var totalDeletedSize = 0L;
                foreach (var delResult in deleteResults)
                {
                    if (context.Metadata[TotalDestDeleteSizeKey] is long tdz)
                        totalDeletedSize = tdz;
                    
                    yield return delResult;
                }
                
                DestDeletedBytes += totalDeletedSize;
            }

            FileArchiveResult result;
            try
            {
                if (context.MoveSourceFile)
                {
                    var sourceFileLength = context.SourceFileInfo.Length;
                    if (!context.DryRun)
                    {
                        File.Move(sourceFileName, context.DestFileFullPath, overwrite: canOverwrite);
                    }
                    
                    if (countBytes)
                        TransferredBytes += sourceFileLength;

                    result = context.CreateResult(updated ? FileResult.MovedUpdated : FileResult.Moved);
                }
                else
                {
                    if (!context.DryRun)
                    {
                        File.Copy(sourceFileName, context.DestFileFullPath, overwrite: canOverwrite);
                    }
                    
                    if (countBytes)
                        TransferredBytes += context.SourceFileInfo.Length;

                    result = context.CreateResult(updated ? FileResult.CopiedUpdated : FileResult.Copied);
                }
            }
            catch (Exception ex)
            {
                result = context.CreateResult(FileResult.Error, exception: ex);
            }
            
            yield return result;
        }
        else
        {
            if (context.Metadata.GetExistingFileName() is { } existingPicName)
            {
               yield return context.CreateResult(FileResult.AlreadyExists, message: existingPicName);
            }
            else
            {
                yield return context.CreateResult(FileResult.Invalid);
            }
        }
    }

    private static IEnumerable<FileArchiveResult> EnsureDestDriveHasEnoughFreeSpace(FileArchiveContext context,
        long requiredFreeSpace)
    {
        var driveInfo = new DriveInfo(context.DestinationRootPath);
        return driveInfo.AvailableFreeSpace <= requiredFreeSpace ? DeleteDestFiles(context, requiredFreeSpace) : [];
    }
    
    private static IEnumerable<FileArchiveResult> DeleteDestFiles(FileArchiveContext context, long bytesToDelete)
    {
        long reclaimedSpace = 0;
        
        foreach (var file in EnumerateFilesForDeletion(context.DestinationBasePath))
        {
            var fileLength = file.Length;
            
            if (!context.Config.DryRun.GetValueOrDefault()) 
                file.Delete();
            
            context.Metadata[TotalDestDeleteSizeKey] = reclaimedSpace += fileLength;
            yield return context.CreateResult(FileResult.DestFileDeleted, message: $"'{file.FullName}' ({fileLength.ToHumanReadableByteSize()})");

           var deleteDirResults = DeleteEmptyDirTree(file.Directory, context);
           foreach (var deleteDirResult in deleteDirResults)
               yield return deleteDirResult;
            
            if (reclaimedSpace > bytesToDelete)
            {
                break;
            }
        }

        yield break;

        static IEnumerable<FileArchiveResult> DeleteEmptyDirTree(DirectoryInfo? dir, FileArchiveContext context)
        {
            while (dir != null && dir.FullName != context.DestinationBasePath)
            {
                var deleteDir = dir; 
                foreach (var file in deleteDir.EnumerateFiles(".DS_Store"))
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // Ignore
                    }
                }
                    
                foreach (var file in deleteDir.EnumerateFiles("._*"))
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // Ignore
                    }
                }

                FileArchiveResult deleteResult = default;
                try
                {
                    var dirName = dir.FullName;
                    dir = dir.Parent;
                    deleteDir.Delete();
                    deleteResult = context.CreateResult(FileResult.DestFileDeleted, message: $"'{dirName}' (FOLDER)");
                }
                catch (Exception)
                {
                    // Ignore
                }

                if (deleteResult.Result == FileResult.DestFileDeleted) 
                    yield return deleteResult;
                else
                    yield break;
            }
            
            try
            {
               
            }
            catch
            {
                // Ignore 
            }
        }
        
        static IEnumerable<FileInfo> EnumerateFilesForDeletion(string path, bool isSubDir = false)
        {
            if (isSubDir) // Dont delete files in the root.
            {
                string[] files = [];
                try
                {
                    files = Directory.GetFiles(path, "*", EnumerationOptionsTopLevelOnly);
                }
                catch
                {
                    // Ignore
                }
                
                foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    if (file.EndsWith("/no_archive", StringComparison.OrdinalIgnoreCase))
                        continue;
                    
                    yield return new FileInfo(file);
                }
            }

            string[] directories = [];
            try
            {
                directories = Directory.GetDirectories(path, "*", EnumerationOptionsTopLevelOnly);
            }
            catch
            {
                // Ignore
            }
             
            foreach (var directory in directories.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var file in EnumerateFilesForDeletion(directory, true))
                {
                   yield return file;
                }
            }
        }
    }
    

    public void Dispose() => Config.MetadataLoaderInstances.Finalize(null);
}

public class MultiFolderArchiver
{
    private readonly IReadOnlyCollection<DirectoryInfo> _folders;
    private readonly ArchiveConfig _config;
    
    public IProgressArchiverResult Result { get; private set; } = ProgressArchiverResult.Default;

    public Func<FileArchiveResult, IFolderArchiverResult, bool>? OnScanResult { get; set; } = null;

    public MultiFolderArchiver(IReadOnlyCollection<DirectoryInfo> folders, ArchiveConfig config)
    {
        _folders = folders;
        _config = config;
    }
    
    public ArchiveConfig Config => _config;

    public IEnumerable<MultiFolderFileArchiveResult> ArchiveTo(string destFolder)
    {
        var scanResults = _config.ReportProgress == true ? ScanAll(destFolder) : null;
        var progressArchiverResult  = new ProgressArchiverResult(scanResults);
        Result = progressArchiverResult;
        
        foreach (var folder in _folders)
        {
            using var folderArchiver = new FolderArchiver(folder.FullName, _config);
            var fileResults = folderArchiver.ArchiveTo(destFolder);
            foreach (var fileArchiveResult in fileResults)
            {
                progressArchiverResult.UpdateProgress(folderArchiver);
                yield return new MultiFolderFileArchiveResult(folder.FullName, fileArchiveResult);
            }
        }
    }

    public IAggregatedArchiverResult ScanAll(string? destFolder)
    {
        var results = new ScannedArchiverResults(_folders.Count);

        foreach (var folder in _folders)
        {
            using var folderArchiver = new FolderArchiver(folder.FullName, _config);
            _ = folderArchiver.ScanSrcFiles(destFolder).All(f => OnScanResult == null || OnScanResult.Invoke(f, folderArchiver));

            IFolderArchiverResult result = folderArchiver;
            results.Add(result);
        }
        
        return results;
    }

    public readonly record struct MultiFolderFileArchiveResult(string Folder, FileArchiveResult FileResult);
}