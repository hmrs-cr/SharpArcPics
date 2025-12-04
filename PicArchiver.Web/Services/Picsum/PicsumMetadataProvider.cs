using PicArchiver.Core.Metadata;

namespace PicArchiver.Web.Services.Picsum;

public class PicsumMetadataProvider : IMetadataProvider
{
    public string Name => "picsum";

    public ValueTask<PictureStats> SetMetadata(PictureStats pictureData) => ValueTask.FromResult(pictureData);
}