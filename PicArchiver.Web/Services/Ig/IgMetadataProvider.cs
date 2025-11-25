using Microsoft.Extensions.Options;
using PicArchiver.Commands.IGArchiver;
using PicArchiver.Core.Metadata;
using PicArchiver.Core.Metadata.Loaders;
using PicArchiver.Extensions;

namespace PicArchiver.Web.Services.Ig;

public class IgMetadataProvider : IMetadataProvider
{
    public string Name => "ig";

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
}

public class IgMetadataConfig
{
    public string PicturesBasePath { get; init; } = "/media/pictures-data";
}