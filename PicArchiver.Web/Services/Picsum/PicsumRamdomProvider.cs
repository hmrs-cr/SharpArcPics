using Microsoft.Extensions.Options;

namespace PicArchiver.Web.Services.Picsum;

public class PicsumRamdomProvider : IRandomProvider
{
    private readonly string url = "https://picsum.photos/1440/1440";
    
    private readonly IHttpClientFactory _httpClientFactory;
    private string _picFolder;

    public PicsumRamdomProvider(IHttpClientFactory httpClientFactory, IOptions<PictureProvidersConfig> config,
        ILogger<PicsumRamdomProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _picFolder = config.Value.PicturesBasePath;
        Directory.CreateDirectory(_picFolder);
        
        logger.LogInformation("Picsum RamdomProvider started. Pic Path: '{PicturesBasePath}'", config.Value.PicturesBasePath);
    }
    
    public async ValueTask<KeyValuePair<string, object?>> GetNextRandomValueAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(url, ct);
        
        response.EnsureSuccessStatusCode();
        
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? DateTime.Now.Ticks + ".jpg";
        var fullFilePath = Path.Combine(_picFolder, fileName);
        
        await using var downloadStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(fullFilePath, FileMode.Create, FileAccess.Write);

        await downloadStream.CopyToAsync(fileStream, ct);
        await fileStream.FlushAsync(ct);
        
        return KeyValuePair.Create(fullFilePath, (object?)null);
    }
}