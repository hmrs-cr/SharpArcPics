using PicArchiver.Core.Metadata;

namespace PicArchiver.Web.Services;

public interface IPictureProvider
{
    string PicturesBasePath { get; }
    ValueTask<string> GetNextRandomValueAsync(CancellationToken ct = default);
    IAsyncEnumerable<string> GetPictureSetIds(ulong setId);
    IAsyncEnumerable<string> GetPictureSetIds(string setId);
    ulong GetPictureIdFromPath(string fullPicturePath);
    
    PictureStats? CreatePictureStats(string? path, ulong pictureId);
}