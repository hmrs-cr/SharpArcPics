namespace PicArchiver.Web.Services.Picsum;

public class PicsumRamdomProvider : IRandomProvider
{
    private readonly string url = "https://picsum.photos/1440";
    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _tempFolder;
    
    public PicsumRamdomProvider(IHttpClientFactory  httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
        _tempFolder = Path.GetTempFileName();
        File.Delete(_tempFolder);
        Directory.CreateDirectory(_tempFolder);
    }
    
    public async ValueTask<string> GetNextRandomValueAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(url, ct);
        
        response.EnsureSuccessStatusCode();
        
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? DateTime.Now.Ticks + ".jpg";
        var fullFilePath = Path.Combine(_tempFolder, fileName);
        
        await using var downloadStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(fullFilePath, FileMode.Create, FileAccess.Write);

        await downloadStream.CopyToAsync(fileStream, ct);
        await fileStream.FlushAsync(ct);
        
        return fullFilePath;
    }
}