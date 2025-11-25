using Microsoft.Extensions.Options;
using PicArchiver.Commands.IGArchiver;
using PicArchiver.Core.Metadata;
using PicArchiver.Core.Metadata.Loaders;
using PicArchiver.Extensions;

namespace PicArchiver.Web.Services.Ig;

public class IgMetadataProvider : IMetadataProvider
{
    private readonly IgMetadataConfig config;

    public IgMetadataProvider(IOptions<IgMetadataConfig> config)
    {
        this.config = config.Value;
    }

    public PictureStats SetMetadata(PictureStats pictureData)
    {
        var igFile = IgFile.Parse(pictureData.FullFilePath);
        
        pictureData.Metadata["IG_UserName"] = igFile.UserName;
        pictureData.Metadata["IG_UserId"] = igFile.UserId.ToString();
        pictureData.Metadata["IG_PictureId"] = igFile.PictureId.ToString();
        pictureData.Metadata["IG_PostId"] = igFile.PostId.ToString();
        pictureData.DownloadName = $"{igFile.UserName}{pictureData.Ext}";

        return pictureData;
    }

    public string? GetRandomPicturePath() => GetRandomCommand.GetRandom(this.config.PicturesBasePath);

    public ulong? GetRandomPictureId() => this.GetRandomPicturePath()?.ComputeHash();
}

public class IgMetadataConfig
{
    public string PicturesBasePath { get; init; } = "/media/pictures-data";
}