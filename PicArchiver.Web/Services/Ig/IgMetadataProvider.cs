using PicArchiver.Commands.IGArchiver;
using PicArchiver.Core.Metadata;
using PicArchiver.Core.Metadata.Loaders;

namespace PicArchiver.Web.Services.Ig;

public class IgMetadataProvider : IMetadataProvider
{
    public string Name => "ig";

    public PictureStats SetMetadata(PictureStats pictureData)
    {
        var igFile = IgFile.Parse(pictureData.FullFilePath);

        if (pictureData.ContextData is IEnumerable<string> allUserName)
        {
            pictureData.Metadata["IG_OtherUserNames"] = string.Join(',', 
                allUserName.Except(Enumerable.Empty<string>().Append(igFile.UserName)));
        }

        pictureData.Metadata["IG_UserName"] = igFile.UserName;
        pictureData.Metadata["IG_UserId"] = igFile.UserId.ToString();
        pictureData.Metadata["IG_PictureId"] = igFile.PictureId.ToString();
        pictureData.Metadata["IG_PostId"] = igFile.PostId.ToString();
        pictureData.DownloadName = $"{igFile.UserName}{pictureData.Ext}";
        pictureData.SourceUrl = $"https://www.instagram.com/{igFile.UserName}/";

        return pictureData;
    }
}