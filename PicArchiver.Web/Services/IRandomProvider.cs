namespace PicArchiver.Web.Services;

public interface IRandomProvider
{
    ValueTask<KeyValuePair<string, object?>> GetNextRandomValueAsync(CancellationToken ct = default);
}