using PicArchiver.Extensions;

namespace PicArchiver.Core.Metadata;

public record PictureStats(string FullFilePath)
{
    public ulong PictureId
    {
        get => field == 0 ? field = FullFilePath.ComputeHash() : field;
        init;
    }

    public string Ext { get; } = Path.GetExtension(FullFilePath);

    public long UpVotes { get; set; }
    public long DownVotes { get; set; }
    public long Favs { get; set; }
    public long Views { get; set; }

    public string? DownloadName { get; set; }
    public string? MimeType { get; set; }
    
    public IDictionary<string, string> Metadata => field ??= new Dictionary<string, string>();

    private long Timestamp { get; set; }
    public string? SourceUrl { get; set; }
    
    public object? ContextData { get; set; }
}