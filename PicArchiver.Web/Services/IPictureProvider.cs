namespace PicArchiver.Web.Services;

public interface IPictureProvider
{
    string PicturesBasePath { get; }
    ValueTask<KeyValuePair<string, object?>> GetNextRandomValueAsync(CancellationToken ct = default);
    IAsyncEnumerable<string> GetPictureSetPaths(ulong setId);
    IAsyncEnumerable<string> GetPictureSetPaths(string setId);
    ulong GetPictureIdFromPath(string fullPicturePath);
}