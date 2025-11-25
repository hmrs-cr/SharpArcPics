using PicArchiver.Core.Metadata;

namespace PicArchiver.Web.Services;

public interface IMetadataProvider
{
    string Name { get; }
    PictureStats SetMetadata(PictureStats pictureData);
}