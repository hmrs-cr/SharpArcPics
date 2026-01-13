using PicArchiver.Core.Metadata;

namespace PicArchiver.Web.Services;

public interface IPictureProvider
{
    string PicturesBasePath { get; }
    ValueTask<string> GetNextRandomValueAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetPictureSetIds(ulong setId);
    Task<IEnumerable<string>> GetPictureSetIds(string setId);
    ulong GetPictureIdFromPath(string fullPicturePath);
    
    PictureStats? CreatePictureStats(string? path, ulong pictureId);
}