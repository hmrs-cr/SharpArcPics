using System.Collections.Concurrent;
using PicArchiver.Commands.IGArchiver;
using PicArchiver.Core.Metadata;
using PicArchiver.Core.Metadata.Loaders;

namespace PicArchiver.Web.Services.Ig;

public class IgMetadataProvider : IMetadataProvider
{
    private readonly ILogger<IgMetadataProvider> _logger;

    public IgMetadataProvider(ILogger<IgMetadataProvider> logger)
    {
        _logger = logger;
    }

    public string Name => "ig";

    public PictureStats SetMetadata(PictureStats pictureData)
    {
        var igFile = IgFile.Parse(pictureData.FullFilePath);

        if (pictureData.ContextData is IEnumerable<string> allUserName)
        {
            pictureData.Metadata["IG_OtherUserNames"] = string.Join(',',
                allUserName.Except(Enumerable.Empty<string>().Append(igFile.UserName)));
        }

        pictureData.Metadata["IG_UserName"] = igFile.UserName;
        pictureData.Metadata["IG_UserId"] = igFile.UserId.ToString();
        pictureData.Metadata["IG_PictureId"] = igFile.PictureId.ToString();
        pictureData.Metadata["IG_PostId"] = igFile.PostId.ToString();
        pictureData.DownloadName = $"{igFile.UserName}{pictureData.Ext}";
        pictureData.SourceUrl = $"https://www.instagram.com/{igFile.UserName}/";

       // _ = SaveAllMetadata(pictureData);

        return pictureData;
    }

    private ConcurrentBag<PictureStats> _pictures = new ConcurrentBag<PictureStats>();
    private int _isSavingMetadata = 0;

    private async Task SaveAllMetadata(PictureStats pictureData)
    {
        _pictures.Add(pictureData);

        if (Interlocked.CompareExchange(ref _isSavingMetadata, 1, 0) == 0)
        {
            try
            {
                while (_pictures.TryTake(out var takenPictureData))
                {
                    await SaveMetadata(takenPictureData);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isSavingMetadata, 0);
            }
        }
    }

    private async Task SaveMetadata(PictureStats pictureData)
    {
        try
        {
            _logger.LogInformation("Saving metadata for {PictureDataFullFilePath}", pictureData.FullFilePath);
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.theyseeyourphotos.com/deductions");
            request.Headers.Add("accept", "application/json, text/plain, */*");
            request.Headers.Add("accept-language", "en-US,en;q=0.9,es-US;q=0.8,es;q=0.7");
            request.Headers.Add("dnt", "1");
            request.Headers.Add("origin", "https://theyseeyourphotos.com");
            request.Headers.Add("priority", "u=1, i");
            request.Headers.Add("referer", "https://theyseeyourphotos.com/");
            request.Headers.Add("sec-ch-ua",
                "\"Chromium\";v=\"142\", \"Google Chrome\";v=\"142\", \"Not_A Brand\";v=\"99\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"macOS\"");
            request.Headers.Add("sec-fetch-dest", "empty");
            request.Headers.Add("sec-fetch-mode", "cors");
            request.Headers.Add("sec-fetch-site", "same-site");
            request.Headers.Add("user-agent",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36");
            var content = new MultipartFormDataContent();

            var filePath = pictureData.FullFilePath;
            content.Add(new StreamContent(File.OpenRead(filePath)), "file");
            content.Add(new StringContent(Path.GetFileName(filePath)), "filename");
            content.Add(new StringContent("en"), "language");
            request.Content = content;
            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var metadataFileName = filePath + ".metadata.json";
                await using var metadataFile = File.OpenWrite(metadataFileName);
                await response.Content.CopyToAsync(metadataFile);

                _logger.LogInformation("Metadata saved to {metadataFile}", metadataFileName);
            }
            else
            {
                this._logger.LogWarning(await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "An error occured while saving metadata");
        }
    }
}