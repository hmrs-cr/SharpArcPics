using PicArchiver.Core.Metadata;

namespace PicArchiver.Web.Services;

public interface IMetadataProvider
{
    string Name { get; }
    ValueTask<PictureStats> SetMetadata(PictureStats pictureData);
}