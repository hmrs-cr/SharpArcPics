using PicArchiver.Core.Metadata;

namespace PicArchiver.Web.Services.Picsum;

public class PicsumMetadataProvider : IMetadataProvider
{
    public string Name => "picsum";

    public PictureStats SetMetadata(PictureStats pictureData) => pictureData;
}