using PicArchiver.Core.Metadata;

namespace PicArchiver.Core.Configs;

public static class DefaultFileArchiveConfig
{
    private static ArchiveConfig DefaultConfig { get; } = new()
    { 
        MoveFiles = false,
        OverrideDestination = false,
        DeleteSourceFileIfDestExists = false,
        SubfolderTemplate = null,
        FileNameTemplate = "{FileName}",
        MetadaLoaders = "Default",
    };

    private static ArchiveConfig IgDefaultConfig { get; } = new()
    { 
        MoveFiles = true,
        OverrideDestination = true,
        DeleteSourceFileIfDestExists = true,
        Recursive = false,
        SubfolderTemplate = "{UserId}",
        FileNameTemplate = "{FileName}",
        MetadaLoaders = "IG,Default",
    };

    private static ArchiveConfig PicDefaultConfig { get; } = new()
    { 
        MoveFiles = false,
        OverrideDestination = true,
        DeleteSourceFileIfDestExists = false,
        Recursive = true,
        SubfolderTemplate = "JPG/{FileYear}/{FileMonth}/{FileYear}-{FileMonth}-{FileDay}",
        SourceFileNameRegExPattern = @"(?i)\.(jpe?g|png|gif|bmp)$",
        FileNameTemplate = "{FileName}",
        MetadaLoaders = "Default,Exif,ChkSum",
    };

    private static ArchiveConfig RawPicDefaultConfig { get; } = new()
    { 
        MoveFiles = false,
        OverrideDestination = true,
        DeleteSourceFileIfDestExists = false,
        Recursive = true,
        SubfolderTemplate = "RAW/{FileYear}/{FileMonth}/{FileYear}-{FileMonth}-{FileDay}",
        SourceFileNameRegExPattern = @"(?i)\.(arw|dng|cr2|orf)$",
        FileNameTemplate = "{FileName}",
        MetadaLoaders = "Default,Exif,ChkSum",
    };

    private static ArchiveConfig VideoDefaultConfig { get; } = new()
    { 
        MoveFiles = false,
        OverrideDestination = true,
        DeleteSourceFileIfDestExists = false,
        Recursive = true,
        SourceFileNameRegExPattern = @"(?i)\.(mov|mp4)$",
        SubfolderTemplate = "Video/{FileYear}/{FileMonth}",
        FileNameTemplate = "{FileName}",
        MetadaLoaders = "Default,Exif,ChkSum",
    };
    
    private static ArchiveConfig RawPicAndVideoDefaultConfig { get; } = new()
    { 
        MoveFiles = false,
        OverrideDestination = true,
        DeleteSourceFileIfDestExists = false,
        SourceFileNameRegExPattern = @"(?i)\.(arw|dng|cr2|orf|mov|mp4)$",
        SubfolderTemplate = "NoMediaKind",
        FileNameTemplate = "{FileName}",
        MetadaLoaders = "Default",
        Recursive = true,
        MediaConfigs = new Dictionary<string, ArchiveConfig>
        {
            [FileMetadata.RawImageMediaKind] = RawPicDefaultConfig,
            [FileMetadata.VideoMediaKind] = VideoDefaultConfig,
        }
    };
    
    private static readonly Dictionary<string, ArchiveConfig> Configs = new()
    {
        ["Default"] = DefaultConfig,
        ["IG"] = IgDefaultConfig,
        ["RawPic"] = RawPicDefaultConfig,
        ["Video"] = VideoDefaultConfig,
        ["Pic"] = PicDefaultConfig,
        ["RPV"] = RawPicAndVideoDefaultConfig,
    };

    public static ArchiveConfig? GetDefaultConfig(string name) => Configs.GetValueOrDefault(name);
    public static IEnumerable<string> GetConfigNames() => Configs.Keys;
}