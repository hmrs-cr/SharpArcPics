using System.Collections;

namespace PicArchiver.Core.Metadata;

public sealed class FileMetadata : IEnumerable<KeyValuePair<string, object?>>
{
    public static readonly string UnknownValue = "Unknown";
    
    public static readonly string FileNameKey = "FileName";
    public static readonly string FileDatetimeKey = "FileDatetime";
    public static readonly string ExifDateTimeKey = "ExifDateTime";
    
    public static readonly string ChecksumKey = "Checksum";
    public static readonly string FileSizeKey = "FileSize";
    public static readonly string ExistingPicNameKey = "ExistingPicName";
    
    public static readonly string FileYearKey = "FileYear";
    public static readonly string FileMonthKey = "FileMonth";
    public static readonly string FileDayKey = "FileDay";
    
    public static readonly string MediaKindKey = "MediaKind";
    
    public static readonly string VideoMediaKind = "video";
    public static readonly string ImageMediaKind = "image";
    public static readonly string RawImageMediaKind = "image-raw";
    
    
    private static readonly Dictionary<string, string> mediaKindByExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cr2"] = RawImageMediaKind,
        ["dng"] = RawImageMediaKind,
        ["orf"] = RawImageMediaKind,
        ["arw"] = RawImageMediaKind,
        ["mov"] = VideoMediaKind,
        ["mp4"] = VideoMediaKind,
    };
    
    private readonly Dictionary<string, object?> _metadata;
    
    public FileMetadata(IDictionary<string, object?>? metadata = null)
    {
        _metadata = metadata == null ? 
            new Dictionary<string, object?>() : 
            new Dictionary<string, object?>(metadata);
    }

    public T? Get<T>(string key, T? defaultValue = default) => 
        _metadata.TryGetValue(key, out var value) && value is T tValue ? tValue : defaultValue;

    public T? Get<T>(params ReadOnlySpan<string> keys)
    {
        foreach (var key in keys)
        {
            if (_metadata.TryGetValue(key, out var value) && value is T tValue) 
                return tValue;
        }
        
        return default;
    }
    
    public bool Exists(string key) => _metadata.ContainsKey(key);

    public object? this[string key]
    {
        get => _metadata.GetValueOrDefault(key);
        set => _metadata[key] = value;
    }

    public IEnumerable<string> Keys => _metadata.Keys;

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, object?>>)_metadata).GetEnumerator();
    
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal void SetMediaKindByFileName(string filePath)
    {
        var extSpan = Path.GetExtension(filePath.AsSpan()).TrimStart('.');
        if (mediaKindByExt.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(extSpan, out var mediaKind))
        {
            _metadata[MediaKindKey] = mediaKind;
        }
    }
}