using PicArchiver.Core.Configs;
using PicArchiver.Extensions;

namespace PicArchiver.Core.Metadata.Loaders;

public sealed class IgMetadataLoader : MetadataLoader
{
    public static readonly string UserIdKey = "UserId";
    public static readonly string PictureIdKey = "PictureId";
    public static readonly string PostTimestampKey = "PostTimestamp";
    public static readonly string UserNameKey = "UserName";
    public static readonly string FileNameKey = "FileName";
    
    public override bool Initialize(FileArchiveContext context)
    {
        context.DeleteSourceFileIfDestExists = context is { DeleteSourceFileIfDestExists: true, DestFileInfo.Exists: true } 
                                               && context.DestFileInfo.Length >= context.SourceFileInfo.Length;
        
        return true;
    }

    public override bool LoadMetadata(string path, FileMetadata metadata)
    {
        var igFile = IgFile.Parse(path);
        if (!igFile.IsValid)
        {
            return false;
        }

        metadata[UserIdKey] = igFile.UserId;
        metadata[PostTimestampKey] = igFile.Timestamp;
        metadata[PictureIdKey] = igFile.PictureId;
        metadata[UserNameKey] = igFile.UserName;
        metadata[FileNameKey] = igFile.FileName;
        
        return true;
    }

    internal static bool DestFileExists(ArchiveConfig config, FileArchiveContext context)
    {
        if (context.DestinationFileExists || 
            context.Metadata[PictureIdKey] is not long pictureId || 
            context.Metadata[UserIdKey] is not long userId)
        {
            return context.DestinationFileExists;
        }
        
        var exists = DestinationPathIfExists(context.DestinationFolderPath, pictureId, userId) != null;
        if (exists)
        {
            context.DeleteSourceFileIfDestExists = config.DeleteSourceFileIfDestExists == true;
        }
        
        return exists;

    }

    public static string? DestinationPathIfExists(string destinationFolderPath, long pictureId, long userId)
    {
        var userIdStr = $"{userId}".AsSpan();
        if (!destinationFolderPath.EndsWith(userIdStr))
        {
            var destinationFolderWithUserIdPath = Path.Join(destinationFolderPath, userIdStr);
            if (Directory.Exists(destinationFolderWithUserIdPath))
            {
                destinationFolderPath = destinationFolderWithUserIdPath;
            }
        }

        var existingDestFile = Directory.EnumerateFiles(destinationFolderPath, $"*_{pictureId}_{userId}.*",
            SearchOption.TopDirectoryOnly).FirstOrDefault();

        return existingDestFile;
    }
}

public record IgFile(
    string FullPath,
    string FileName,
    string UserName,
    long UserId,
    long PictureId,
    long Timestamp,
    bool IsMetadata)
{
    private static readonly IgFile InvalidIgFile = new IgFile("", "", "", 0, 0, 0, false);

    public const char Separator = '_';
    public const string MetadataExtension = ".metadata.json";
    public const string CompressedExtenson = ".xz";
    public const string JsonExtenson = ".json";


    public bool IsValid => !string.IsNullOrEmpty(FileName) && !string.IsNullOrEmpty(UserName)
                                                           && UserId > 100 
                                                           && PictureId > 1000
                                                           && (IsMetadata || !FileName.EndsWith(JsonExtenson))
                                                           && !FileName.EndsWith(CompressedExtenson);
    
    public DateTimeOffset DateTime => Timestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(Timestamp) : File.GetCreationTime(FullPath);
    
    public ulong PictureDbId => field == 0 ? field = FileName.ComputeFileNameHash() : field;
    
    public string? DestinationPathIfExists(string destinationFolderPath) => 
        IgMetadataLoader.DestinationPathIfExists(destinationFolderPath, PictureId, UserId);
    
    public bool ExistsAtDestination(string destinationFolderPath) => DestinationPathIfExists(destinationFolderPath) != null;
    
    public static IgFile Parse(string fileName)
    {
        var fullPath = fileName;
        var fileNameSpan = Path.GetFileName(fileName).AsSpan();
        fileName = fileNameSpan.ToString();

        var isMetadata = fileNameSpan.EndsWith(MetadataExtension);
        if (isMetadata)
        {
            fileNameSpan = fileNameSpan.TrimEnd(MetadataExtension);
        }
        
        var dotIndex = fileNameSpan.LastIndexOf("_n."); 
        dotIndex = dotIndex < 10 ? fileNameSpan.LastIndexOf(" (1).") : dotIndex;
        dotIndex = dotIndex == -1 ? fileNameSpan.LastIndexOf('.') : dotIndex;
        dotIndex = dotIndex == -1 ? fileNameSpan.Length : dotIndex;
        
        fileNameSpan = fileNameSpan.Slice(0, dotIndex);
        
        var userIdStartIndex = fileNameSpan.LastIndexOf(Separator);
        if (userIdStartIndex == -1) return InvalidIgFile;
        var userIdSpan = fileNameSpan.Slice(userIdStartIndex + 1).TrimEnd(JsonExtenson + CompressedExtenson);
        fileNameSpan = fileNameSpan.Slice(0, userIdStartIndex);
        
        var pictureIdStartIndex = fileNameSpan.LastIndexOf(Separator);
        if (pictureIdStartIndex == -1) return InvalidIgFile;
        var pictureIdSpan = fileNameSpan.Slice(pictureIdStartIndex + 1);
        fileNameSpan = fileNameSpan.Slice(0, pictureIdStartIndex);
        
        var timestampStartIndex = fileNameSpan.LastIndexOf(Separator);
        var timestampSpan = ReadOnlySpan<char>.Empty;
        if (timestampStartIndex > -1)
        {
            timestampSpan = fileNameSpan.Slice(timestampStartIndex + 1);
            fileNameSpan = fileNameSpan.Slice(0, timestampStartIndex);
        }

        var uid = long.TryParse(userIdSpan, out var userId) ? userId : 0;
        var pid = long.TryParse(pictureIdSpan, out var pictureId) ? pictureId : 0;
        var time = long.TryParse(timestampSpan, out var postId) ? postId : 0;

        if (time == 0 && pid > 1268219471 && pid <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            // If picture id looks like a date then probably is a date
            time = pid;
            pid = uid;
            uid = 0;
        }
        
        return new IgFile(
            fullPath, 
            fileName, 
            fileNameSpan.ToString(),
            uid,
            pid,
            time,
            isMetadata);
    }
}