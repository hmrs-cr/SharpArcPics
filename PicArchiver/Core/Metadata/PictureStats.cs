using PicArchiver.Extensions;

namespace PicArchiver.Core.Metadata;

public record PictureStats(string FullFilePath)
{
    public ulong PictureId
    {
        get => field == 0 ? field = FullFilePath.ComputeHash() : field;
        private set => field = value;
    }

    public uint UpVotes { get; set; }
    public uint DownVotes { get; set; }
    public uint Favs { get; set; }
    public uint Views { get; set; }

    private long Timestamp { get; set; }
}