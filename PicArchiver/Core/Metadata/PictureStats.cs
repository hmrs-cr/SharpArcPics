using PicArchiver.Extensions;

namespace PicArchiver.Core.Metadata;

public record PictureStats(string FullFilePath)
{
    public ulong PictureId
    {
        get => field == 0 ? field = FullFilePath.ComputeHash() : field;
        private set => field = value;
    }

    public string Ext { get; } = Path.GetExtension(FullFilePath);

    public uint UpVotes { get; set; }
    public uint DownVotes { get; set; }
    public uint Favs { get; set; }
    public uint Views { get; set; }

    public string? DownloadName { get; set; }
    public string? MimeType { get; set; }
    
    public IDictionary<string, string> Metadata => field ??= new Dictionary<string, string>();

    private long Timestamp { get; set; }
    public string? SourceUrl { get; set; }
}