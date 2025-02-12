using System.Collections;
using System.Diagnostics;
using PicArchiver.Core.Configs;
using PicArchiver.Core.Metadata.Loaders;
using PicArchiver.Extensions;

namespace PicArchiver.Core;

public interface IFolderArchiverResult
{
    int FailedFilesCount { get; }
    int SrcDeletedFilesCount { get; }
    int DestDeletedFilesCount { get; } 
    long SrcDeleteBytes { get; }
    long DestDeleteBytes { get; }
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
    public int SrcDeletedFilesCount { get; private set; }
    
    public int DestDeletedFilesCount { get; private set; }
    public long SrcDeleteBytes { get; private set; }
    public long DestDeleteBytes { get; private set; }
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
        SrcDeletedFilesCount += folderArchiverResult.SrcDeletedFilesCount;
        UpdatedFilesCount += folderArchiverResult.UpdatedFilesCount;
        MovedFilesCount += folderArchiverResult.MovedFilesCount;
        DuplicatedFileCount += folderArchiverResult.DuplicatedFileCount;
        InvalidFileCount += folderArchiverResult.InvalidFileCount;
        CopiedFileCount += folderArchiverResult.CopiedFileCount;
        ProcessedFilesCount += folderArchiverResult.ProcessedFilesCount;
        TotalFilesCount += folderArchiverResult.TotalFilesCount;
        TransferredBytes += folderArchiverResult.TransferredBytes;
        DestDeletedFilesCount += folderArchiverResult.DestDeletedFilesCount;
        SrcDeleteBytes += folderArchiverResult.SrcDeleteBytes;
        DestDeleteBytes += folderArchiverResult.DestDeleteBytes;
        
        ElapsedTime = Stopwatch.GetElapsedTime(_startTime);

        _results.Add(folderArchiverResult);
    }

    public IEnumerator<IFolderArchiverResult> GetEnumerator() => _results.GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public class FolderArchiver : IDisposable, IFolderArchiverResult
{
    private const string TotalDestDeleteSizeKey = "TotalDestDeletedSize";
    
    public int FailedFilesCount { get; private set; }

    public int SrcDeletedFilesCount { get; private set; }
    public int DestDeletedFilesCount { get; private set; }
    
    public int UpdatedFilesCount { get; private set; }

    public int MovedFilesCount { get; private set; }
    
    public int DuplicatedFileCount { get; private set; }

    public int InvalidFileCount { get; private set; }

    public int CopiedFileCount { get; private set; }
    
    public int ProcessedFilesCount => MovedFilesCount + UpdatedFilesCount + SrcDeletedFilesCount + CopiedFileCount;
    
    public int TotalFilesCount { get; private set; }
    
    public TimeSpan ElapsedTime { get; private set; }
    
    public long TransferredBytes { get; private set; }
    public long SrcDeleteBytes { get; private set; }
    public long DestDeleteBytes { get; private set; }

    public string SourceFolder { get; }
    
    public ArchiveConfig Config { get; }
    
    private static readonly EnumerationOptions EnumerationOptionsTopLevelOnly = new() { RecurseSubdirectories = false, IgnoreInaccessible = true };
    private static readonly EnumerationOptions EnumerationOptionsRecursive = new() { RecurseSubdirectories = true, IgnoreInaccessible = true };

    public FolderArchiver(string sourceFolder, ArchiveConfig config)
    {
        SourceFolder = sourceFolder;
        Config = config;
    }
    
    public IEnumerable<FileArchiveResult> ArchiveTo(string destFolder)
    {
        var ts = Stopwatch.GetTimestamp();
        
        var files = EnumerateFiles(SourceFolder, Config.Recursive == true);
        foreach (var file in files)
        {
            var results = ProcessFile(file, destFolder);
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
        var files = Directory.GetFiles(folderPath, "*", EnumerationOptionsTopLevelOnly).OrderBy(f => f);
        foreach (var file in files)
            yield return file;

        if (recurseSubdirectories)
        {
            var directories = Directory.GetDirectories(folderPath, "*", EnumerationOptionsRecursive).OrderBy(d => d);
            foreach (var directory in directories)
            {
                if (File.Exists(Path.Join(directory, "no_archive")))
                    continue;
                
                files = Directory.GetFiles(directory, "*", EnumerationOptionsTopLevelOnly).OrderBy(f => f);
                foreach (var file in files)
                    yield return file;
            }
        }
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
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (result != FileResult.DestFileDeleted)
        {
            TotalFilesCount++;
        }
    }

    private IEnumerable<FileArchiveResult> ProcessFile(string sourceFileName, string destDirectory)
    {
        using var context = new FileArchiveContext(sourceFileName, destDirectory, Config);
        if (context.IsValid)
        {
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
                        SrcDeleteBytes += sourceFileLength;
                    }

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
                
                DestDeleteBytes += totalDeletedSize;
            }

            FileArchiveResult result;
            try
            {
                if (context.MoveSourceFile)
                {
                    if (!context.DryRun)
                    {
                        var sourceFileLength = context.SourceFileInfo.Length;
                        File.Move(sourceFileName, context.DestFileFullPath, overwrite: canOverwrite);
                        TransferredBytes += sourceFileLength;
                    }

                    result = context.CreateResult(updated ? FileResult.MovedUpdated : FileResult.Moved);
                }
                else
                {
                    if (!context.DryRun)
                    {
                        File.Copy(sourceFileName, context.DestFileFullPath, overwrite: canOverwrite);
                        TransferredBytes += context.SourceFileInfo.Length;
                    }

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
        // TODO: Extract drive letter if in Windows.
        var driveInfo = new DriveInfo(context.DestinationBasePath);
        if (driveInfo.AvailableFreeSpace <= requiredFreeSpace)
        {
            return DeleteDestFiles(context, requiredFreeSpace);
        }

        return [];
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
                catch (Exception e)
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