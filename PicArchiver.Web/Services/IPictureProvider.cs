using PicArchiver.Core.Metadata;

namespace PicArchiver.Web.Services;

public interface IPictureProvider
{
    string PicturesBasePath { get; }
    ValueTask<string> GetNextRandomValueAsync(CancellationToken ct = default);
    IAsyncEnumerable<string> GetPictureSetPaths(ulong setId);
    IAsyncEnumerable<string> GetPictureSetPaths(string setId);
    ulong GetPictureIdFromPath(string fullPicturePath);
    
    PictureStats? CreatePictureStats(string? path, ulong pictureId);
}