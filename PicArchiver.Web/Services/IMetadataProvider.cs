using PicArchiver.Core.Metadata;

namespace PicArchiver.Web.Services;

public interface IMetadataProvider
{
    PictureStats SetMetadata(PictureStats pictureData);
    string? GetRandomPicturePath();
    ulong? GetRandomPictureId();
}