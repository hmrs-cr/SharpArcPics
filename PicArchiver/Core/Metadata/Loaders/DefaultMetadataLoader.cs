namespace PicArchiver.Core.Metadata.Loaders;

public sealed class DefaultMetadataLoader : MetadataLoader
{
    public override bool Initialize(FileArchiveContext context)
    {
        context.OverrideDestinationFile =  context is { OverrideDestinationFile: true, DestFileInfo.Exists: true, SourceFileInfo.Length: > 1024 }
                                           && context.DestFileInfo.Length != context.SourceFileInfo.Length;
        return base.Initialize(context);
    }

    public override bool LoadMetadata(string path, FileMetadata metadata)
    {
        var date = File.GetCreationTime(path);
        var fileName = Path.GetFileName(path);
        metadata[FileMetadata.FileDatetimeKey] = date;
        metadata[FileMetadata.FileYearKey] = date.Year;
        metadata[FileMetadata.FileMonthKey] = date.Month.ToString("00");
        metadata[FileMetadata.FileDayKey] = date.Day.ToString("00");
        metadata[FileMetadata.FileNameKey] = fileName;
        metadata.SetMediaKindByFileName(fileName);
        
        return true;
    }
}

public static class DefaultMetadataExtensions
{
    public static DateTime GetFileDateTime(this FileMetadata metadata) =>
        metadata.Get<DateTime>(FileMetadata.ExifDateTimeKey, FileMetadata.FileDatetimeKey);
    
    public static string? GetMediaKind(this FileMetadata metadata) =>
        metadata.Get<string>(FileMetadata.MediaKindKey);
}