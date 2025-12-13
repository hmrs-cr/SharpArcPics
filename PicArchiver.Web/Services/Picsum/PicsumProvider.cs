using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PicArchiver.Core.Metadata;
using PicArchiver.Extensions;

namespace PicArchiver.Web.Services.Picsum;

public class PicsumProvider : IPictureProvider
{
    private const int MaxCachedImages = 128;
    
    private readonly string url = "https://picsum.photos/1440/1440";
    
    private readonly ConcurrentQueue<string> _localPathList = [];
    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PicsumProvider> _logger;

    public PicsumProvider(IHttpClientFactory httpClientFactory, IOptions<PictureProvidersConfig> config,
        ILogger<PicsumProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        PicturesBasePath = config.Value.PicturesBasePath;
        Directory.CreateDirectory(PicturesBasePath);
        
        _ = FillLocalPathList();
        
        _logger.LogInformation("Picsum Provider started. Pic Path: '{PicturesBasePath}'", config.Value.PicturesBasePath);
    }
    
    public string PicturesBasePath { get; }

    public async ValueTask<string> GetNextRandomValueAsync(CancellationToken ct = default)
    {
        _ = FillLocalPathList();
        
        if (!_localPathList.IsEmpty && _localPathList.TryDequeue(out var localPath))
        {
            return localPath;
        }
        
        localPath = await GetNextRandomValueInternalAsync(ct);
        return localPath;
    }

    private async Task FillLocalPathList()
    {
        while (MaxCachedImages - _localPathList.Count > 0)
        {
            try
            {
                var localPath = await GetNextRandomValueInternalAsync();
                _localPathList.Enqueue(localPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error filling local path list.");
            }
        }
    }
    
    private async ValueTask<string> GetNextRandomValueInternalAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(url, ct);
        
        response.EnsureSuccessStatusCode();
        
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? DateTime.Now.Ticks + ".jpg";
        var fullFilePath = Path.Combine(PicturesBasePath, fileName);
        
        await using var downloadStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(fullFilePath, FileMode.Create, FileAccess.Write);

        await downloadStream.CopyToAsync(fileStream, ct);
        await fileStream.FlushAsync(ct);

        return fullFilePath;
    }

    public IAsyncEnumerable<string> GetPictureSetPaths(ulong setId)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<string> GetPictureSetPaths(string setId)
    {
        throw new NotImplementedException();
    }

    public ulong GetPictureIdFromPath(string fullPicturePath) => fullPicturePath.ComputeFileNameHash();
    
    public PictureStats? CreatePictureStats(string? path, ulong pictureId) => path is null ? null : new PictureStats(path, pictureId);
}