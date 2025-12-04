using PicArchiver.Core.Configs;

namespace PicArchiver.Core.Metadata.Loaders;

public sealed class IgMetadataLoader : MetadataLoader
{
    public static readonly string UserIdKey = "UserId";
    public static readonly string PictureIdKey = "PictureId";
    public static readonly string PostIdKey = "PostId";
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
        metadata[PostIdKey] = igFile.PostId;
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
        
        var existingDestFile = Directory.EnumerateFiles(context.DestinationFolderPath,
            $"*_{pictureId}_{userId}.*", SearchOption.TopDirectoryOnly).FirstOrDefault();

        var exists = existingDestFile != null;
        if (exists)
        {
            context.DeleteSourceFileIfDestExists = config.DeleteSourceFileIfDestExists == true;
        }
        
        return exists;

    }
}

public readonly record struct IgFile(string FullPath, string FileName, string UserName, long UserId, long PictureId, long PostId)
{
    public static readonly char Separator = '_';
    
    public static readonly string MetadataExtension = ".metadata.json";
    
    public bool IsValid => !string.IsNullOrEmpty(FileName) && !string.IsNullOrEmpty(UserName)
                                                           && UserId > 100 
                                                           && PictureId > 1000 
                                                           && PostId > 1000;
    
    public static IgFile Parse(string fileName)
    {
        var fullPath = fileName;
        var fileNameSpan = Path.GetFileName(fileName).AsSpan();
        fileName = fileNameSpan.ToString();
        fileNameSpan = fileNameSpan.TrimEnd(MetadataExtension);
        
        var dotIndex = fileNameSpan.LastIndexOf("_n."); 
        dotIndex = dotIndex < 10 ? fileNameSpan.LastIndexOf(" (1).") : dotIndex;
        dotIndex = dotIndex == -1 ? fileNameSpan.LastIndexOf('.') : dotIndex;
        dotIndex = dotIndex == -1 ? fileNameSpan.Length : dotIndex;
        
        fileNameSpan = fileNameSpan.Slice(0, dotIndex);
        
        var userIdStartIndex = fileNameSpan.LastIndexOf(Separator);
        if (userIdStartIndex == -1) return default;
        var userIdSpan = fileNameSpan.Slice(userIdStartIndex + 1);
        fileNameSpan = fileNameSpan.Slice(0, userIdStartIndex);
        
        var pictureIdStartIndex = fileNameSpan.LastIndexOf(Separator);
        if (pictureIdStartIndex == -1) return default;
        var pictureIdSpan = fileNameSpan.Slice(pictureIdStartIndex + 1);
        fileNameSpan = fileNameSpan.Slice(0, pictureIdStartIndex);
        
        var postIdStartIndex = fileNameSpan.LastIndexOf(Separator);
        if (postIdStartIndex == -1) return default;
        var postIdSpan = fileNameSpan.Slice(postIdStartIndex + 1);
        fileNameSpan = fileNameSpan.Slice(0, postIdStartIndex);

        return new IgFile
        {
            FullPath = fullPath,
            FileName = fileName,
            UserName = fileNameSpan.ToString(),
            UserId = long.TryParse(userIdSpan, out var userId) ? userId : 0,
            PictureId = long.TryParse(pictureIdSpan, out var pictureId) ? pictureId : 0,
            PostId = long.TryParse(postIdSpan, out var postId) ? postId : 0
        };
    }
}