using PicArchiver.Core.Configs;
using PicArchiver.Core.Metadata;
using PicArchiver.Core.Metadata.Loaders;
using PicArchiver.Extensions;

namespace PicArchiver.Core;

public class FileArchiveContext : IDisposable
{
    private FileInfo? _sourceFileInfo = null;
    private FileInfo? _destFileInfo = null;
    private string? _destinationRootPath = null;
    
    public ArchiveConfig Config { get; }

    public FileMetadata Metadata { get; }
    
    public string SourceFileFullPath { get; }
    
    public string DestinationBasePath { get; }

    public string DestinationRootPath =>
        _destinationRootPath ??= OperatingSystem.IsWindows() ? 
                                 Path.GetPathRoot(Path.GetFullPath(DestinationBasePath)) ?? DestinationBasePath : 
                                 DestinationBasePath;
    
    public string DestFileFullPath { get; }
    
    public string DestinationFolderPath { get; }

    public bool DestinationFileExists { get; set; }

    public bool MoveSourceFile { get;  set; }
    
    public bool DeleteSourceFileIfDestExists { get; set; }

    public bool OverrideDestinationFile { get; set; }
    
    public bool DryRun { get; set; }
    
    public bool IsValid { get; }

    public FileInfo SourceFileInfo => _sourceFileInfo ??= new FileInfo(SourceFileFullPath);

    public FileInfo DestFileInfo => _destFileInfo ??= new FileInfo(DestFileFullPath);
    
    public ICollection<IMetadataLoader>? MetadataLoaders { get; }

    internal FileArchiveResult CreateResult(FileResult result, string? message = null, Exception? exception = null)
        => new FileArchiveResult(this, result, message, exception);

    internal IEnumerable<FileArchiveResult> CreateResults(FileResult result, string? message = null,
        Exception? exception = null)
    {
        yield return CreateResult(result, message, exception);
    }

    internal FileArchiveContext(string sourceFileName, string destinationFolderPath, ArchiveConfig config)
    {
        Config = config;
        SourceFileFullPath = sourceFileName;
        DestinationBasePath = destinationFolderPath;
        DestinationFolderPath = destinationFolderPath;
        DestFileFullPath = string.Empty;
        
        Metadata = new FileMetadata(Config.TokenValues);
        MetadataLoaders = Config.MetadataLoaderInstances;
        
        IsValid = SourceFileNameMatchRegEx(SourceFileFullPath, Config) && MetadataLoaders.LoadMetadata(this);
        if (IsValid)
        {
            if (Config.MediaConfigs?.GetValueOrDefault(Metadata.GetMediaKind() ?? string.Empty) is { } mediaConfig)
            {
                Config = mediaConfig;
            }
            
            DestinationFolderPath = ResolveDestSubFolder(DestinationBasePath, Config);
            DestFileFullPath = Path.Combine(DestinationFolderPath, ResolveDestFileName(SourceFileFullPath, Config));
            DestinationFileExists = File.Exists(DestFileFullPath);
            OverrideDestinationFile = Config.OverrideDestination ?? false;
            DeleteSourceFileIfDestExists = Config.DeleteSourceFileIfDestExists ?? false;
            MoveSourceFile = Config.MoveFiles ?? false;
            DryRun = Config.DryRun ?? false;

            IsValid = MetadataLoaders.Initialize(this);
        }
    }

    private string ResolveDestSubFolder(string destinationFolderPath, ArchiveConfig? config)
    {
        var subFolderName = config?.SubfolderTemplate?.ResolveTokens(Metadata) ?? string.Empty;
        return Directory.CreateDirectory(Path.Combine(destinationFolderPath, subFolderName)).FullName;
    }

    private string ResolveDestFileName(string sourceFileName, ArchiveConfig? config)
    {
        var fileName = config?.FileNameTemplate?.ResolveTokens(Metadata) ?? Path.GetFileName(sourceFileName);
        return fileName;
    }
    
    private static bool SourceFileNameMatchRegEx(string sourceFileName, ArchiveConfig? config) =>
        config?.SourceFileNameRegEx is null || config.SourceFileNameRegEx.Match(sourceFileName).Success;

    public void Dispose() => MetadataLoaders.Finalize(this);
}

public readonly record struct FileArchiveResult
{
    public FileArchiveContext Context { get; }
    public FileResult Result { get; }
    public Exception? Exception { get; }
    public string? Message { get; }

    public FileArchiveResult(
        FileArchiveContext context, 
        FileResult result,
        string? message = null,
        Exception? exception = null)
    {
        Context = context;
        Result = result;
        Exception = exception;
        Message = message;
    }
}

public enum FileResult
{
    Undefined,
    Moved,
    Copied,
    CopiedUpdated,
    MovedUpdated,
    SourceFileDeleted,
    DestFileDeleted,
    Error,
    
    // Does not meet filter or metadata criteria.
    Invalid,
    
    // Already exists in dest.
    AlreadyExists,
}