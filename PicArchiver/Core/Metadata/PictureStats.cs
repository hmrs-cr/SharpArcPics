using PicArchiver.Core.DataAccess;
using PicArchiver.Extensions;

namespace PicArchiver.Core.Metadata;

public record PictureStats(string FullFilePath, ulong PictureId)
{
    public string Ext { get; } = Path.GetExtension(FullFilePath);

    public long UpVotes { get; set; }
    public long DownVotes { get; set; }
    public long Favs { get; set; }
    public long Views { get; set; }
    
    public string? Description { get; set; }
    public string? Keywords { get; set; }
    public DateTime? Date { get; set; }
    public DateTime? BackupDate { get; set; }
    public string? Autor { get; set; }

    public string? DownloadName { get; set; }
    public string? MimeType { get; set; }
    
    public IDictionary<string, string> Metadata => field ??= new Dictionary<string, string>();

    private long Timestamp { get; set; }
    public string? SourceUrl { get; set; }
    
    public object? ContextData { get; set; }

    public PictureStats AssignMetadata(PictureMetaData? metaData)
    {
        if (metaData != null)
        {
            Description = metaData.Description;
            Keywords = metaData.Keywords;
            BackupDate = metaData.DateAdded;
            
            if (metaData.Description != null)
            {
                Metadata["Description"] = metaData.Description;
            }

            if (metaData.Keywords != null)
            {
                Metadata["Keywords"] = metaData.Keywords;
            }

            /*if (Autor != null)
            {
                Metadata["Autor"] = "TODO";
            }*/
            
            if (metaData.DateAdded != null)
            {
                Metadata["BackupDate"] =metaData.DateAdded.Value.ToString("s");
            }
        }
        
        return this;
    }
}