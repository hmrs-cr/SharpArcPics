using PicArchiver.Core.Metadata;

namespace PicArchiver.Web.Services;

public interface IPictureService
{
    Task<PictureStats?> GetRandomPictureData(Guid requestUserId);
    Task<PictureStats?> GetPictureData(ulong pictureId, Guid? requestUserId, bool onlyIfNotViewed = false);
    Task<int> IncrementPictureView(ulong pictureId, Guid requestUserId);
    Task<int> Upvote(ulong pictureId, Guid uid, bool remove = false);
    Task<int> Downvote(ulong pictureId, Guid uid, bool remove = false);
    Task<int> Favorite(ulong pictureId, Guid uid, bool remove = false);

    Task<string?> GetPictureThumbPath(ulong pictureId);
    Task<ICollection<string>> GetTopRatedPicturesIds();
    Task<ICollection<string>> GetLowRatedPicturesIds();
    Task<ICollection<string>> GetImageSet(ulong setId);
    Task<ICollection<string>> GetDeletedIds();
    Task<ICollection<string>> GetIncomingIds();
    
    Task<bool> DeletePicture(ulong pictureId);
}