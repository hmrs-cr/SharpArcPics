using System.Text.Json;
using PicArchiver.Core.Metadata;
using PicArchiver.Core.Metadata.Loaders;

namespace PicArchiver.Web.Services.Ig;

public class IgMetadataProvider : IMetadataProvider
{
    private static readonly JsonSerializerOptions jssOptions = new JsonSerializerOptions()
    {
        PropertyNameCaseInsensitive = true
    };
    
    private readonly ILogger<IgMetadataProvider> _logger;

    public IgMetadataProvider(ILogger<IgMetadataProvider> logger)
    {
        _logger = logger;
    }

    public string Name => "ig";
    
    public static bool IsValidFilePath(string fileName)
    {
        return !fileName.EndsWith(".mp4", StringComparison.InvariantCultureIgnoreCase) &&
               !fileName.EndsWith(IgFile.MetadataExtension) &&
               !File.GetAttributes(fileName).HasFlag(FileAttributes.Hidden);
    }

    public async ValueTask<PictureStats> SetMetadata(PictureStats pictureData)
    {
        var igFile = IgFile.Parse(pictureData.FullFilePath);
        var metadataFile = igFile.FullPath + IgFile.MetadataExtension;

        var igMetadata = string.Empty;
        if (File.Exists(metadataFile))
        {
            await using var stream = File.Open(metadataFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var igmd = await JsonSerializer.DeserializeAsync<IgMetadataRoot>(stream, jssOptions);
            igMetadata = igmd.ToString();
        }

        if (pictureData.ContextData is IEnumerable<string> allUserName)
        {
            pictureData.Metadata["IG_OtherUserNames"] = string.Join(',',
                allUserName.Except(Enumerable.Empty<string>().Append(igFile.UserName)));
        }

        if (!string.IsNullOrEmpty(igMetadata))
        {
            pictureData.Metadata["Description"] = igMetadata;
        }

        pictureData.Metadata["IG_UserName"] = igFile.UserName;
        pictureData.Metadata["IG_UserId"] = igFile.UserId.ToString();
        pictureData.Metadata["IG_PictureId"] = igFile.PictureId.ToString();
        pictureData.Metadata["IG_PostId"] = igFile.PostId.ToString();
        pictureData.DownloadName = $"{igFile.UserName}{pictureData.Ext}";
        pictureData.SourceUrl = $"https://www.instagram.com/{igFile.UserName}/";
        
        return pictureData;
    }
    
    internal readonly struct IgMetadataRoot
    {
        public bool Success { get; init; }
        public MetadataData Data { get; init; }

        public override string ToString() => !Success ? string.Empty : Data.ToString();
        
        internal readonly struct MetadataData
        {
            public MetadataTable Table { get; init; }
            
            public override string ToString() => Table.ToString();
            
            internal readonly struct MetadataTable
            {
                public string Clothing  { get; init; }
                public string People { get; init; }
                public string Emotions { get; init; }
                public string Race { get; init; }

                public override string ToString() => $"{People} ({Race}) in {Clothing}. {Emotions}";
            }
        }
    }
}