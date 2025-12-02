namespace PicArchiver.Web.Services;

public interface IPictureProvider
{
    ValueTask<KeyValuePair<string, object?>> GetNextRandomValueAsync(CancellationToken ct = default);
    IAsyncEnumerable<string> GetPictureSetPaths(ulong setId);
    IAsyncEnumerable<string> GetPictureSetPaths(string setId);
}