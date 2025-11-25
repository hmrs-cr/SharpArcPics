namespace PicArchiver.Web.Services;

public interface IRandomProvider
{
    ValueTask<string> GetNextRandomValueAsync(CancellationToken ct = default);
}