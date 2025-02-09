using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.FileType;
using MetadataExtractor.Formats.QuickTime;

namespace PicArchiver.Core.Metadata.Loaders;

public sealed class ExifMetadataLoader : MetadataLoader
{
    public static readonly string CameraMakerKey = "CameraMaker";
    public static readonly string CameraModelKey = "CameraModel";
    public static readonly string LensModelKey = "LensModel";
    public static readonly string CopyrightKey = "Copyright";
    public static readonly string ArtistKey = "Artist";


    private static readonly ICollection<string> RawImageNames = [
        //"TIFF",
        "ARW",
        "CRW",
        "CR2",
        "NEF",
        "ORF",
        "RAF",
        "RW2",
        "CRX",
    ];
    
    public override bool LoadMetadata(string path, FileMetadata metadata)
    {
        var directories = ReadMetadata(path);
        
        /*foreach (var directory in directories)
        {
            foreach (var tag in directory.Tags)
            {
                metadata[$"{directory.Name}-{tag.Name}"] = tag.Description;
            }
        }*/
        
        var ifdDirectory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var quickTimeMetadataHeaderDirectory = directories.OfType<QuickTimeMetadataHeaderDirectory>().FirstOrDefault();
        var fileTypeDirectory = directories.OfType<FileTypeDirectory>().FirstOrDefault();
        
        var cameraMaker = ifdDirectory?.GetObject(ExifDirectoryBase.TagMake)?.ToString() ??
                          quickTimeMetadataHeaderDirectory?.GetObject(QuickTimeMetadataHeaderDirectory.TagAndroidManufacturer)?.ToString();
        
        var cameraModel = ifdDirectory?.GetObject(ExifDirectoryBase.TagModel)?.ToString() ??
                          quickTimeMetadataHeaderDirectory?.GetObject(QuickTimeMetadataHeaderDirectory.TagAndroidModel)?.ToString();
        
        var lensModel = subIfdDirectory?.GetObject(ExifDirectoryBase.TagLensModel)?.ToString();
        
        var copyright = ifdDirectory?.GetObject(ExifDirectoryBase.TagCopyright)?.ToString();
        var artist = ifdDirectory?.GetObject(ExifDirectoryBase.TagArtist)?.ToString();

        var mimeType = fileTypeDirectory?.GetObject(FileTypeDirectory.TagDetectedFileMimeType)?.ToString();
        var mediaName = fileTypeDirectory?.GetObject(FileTypeDirectory.TagDetectedFileTypeName)?.ToString();
        if (mimeType == null && mediaName != null && RawImageNames.Contains(mediaName))
        {
            mimeType = FileMetadata.RawImageMediaKind;
        }

        if (mimeType != null)
        {
            var i = mimeType.IndexOf('/');
            if (i < 0) i = mimeType.Length;
            metadata[FileMetadata.MediaKindKey] = mimeType?.Substring(0, i);
        }

        var dateTime = GetDateTime(ifdDirectory, subIfdDirectory, quickTimeMetadataHeaderDirectory);
        if (dateTime != null)
        {
            metadata[FileMetadata.ExifDateTimeKey] = dateTime;
            metadata[FileMetadata.FileYearKey] = dateTime.Value.Year;
            metadata[FileMetadata.FileMonthKey] = dateTime.Value.Month.ToString("00");
            metadata[FileMetadata.FileDayKey] = dateTime.Value.Day.ToString("00");
        }
        
        metadata[CameraMakerKey] = cameraMaker ?? FileMetadata.UnknownValue;
        metadata[CameraModelKey] = cameraModel ?? FileMetadata.UnknownValue;
        metadata[LensModelKey] = lensModel ?? FileMetadata.UnknownValue;
        metadata[CopyrightKey] = copyright ?? FileMetadata.UnknownValue;
        metadata[ArtistKey] = artist ?? FileMetadata.UnknownValue;
        
        return true;
    }

    private IReadOnlyList<MetadataExtractor.Directory> ReadMetadata(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ImageMetadataReader.ReadMetadata(stream);
        }
        catch (Exception)
        {
            // Ignore
            return Array.Empty<MetadataExtractor.Directory>();
        }
    }

    private DateTime? GetDateTime(params ReadOnlySpan<MetadataExtractor.Directory?> directories)
    {
        foreach (var directory in directories)
        {
            if (directory?.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dateTime) == true)
            {
                return dateTime;
            }
            
            if (directory?.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out dateTime) == true)
            {
                return dateTime;
            }
            
            if (directory?.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out dateTime) == true)
            {
                return dateTime;
            }

            if (directory?.TryGetDateTime(QuickTimeMetadataHeaderDirectory.TagCreationDate, out dateTime) == true)
            {
                return dateTime;
            }
        }
        
        return null;
    }
}