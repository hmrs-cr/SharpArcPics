using PicArchiver.Core.Metadata;

namespace PicArchiver.Web.Services.picsum;

public class PicsumMetadataProvider : IMetadataProvider
{
    private readonly string _tempFolder;

    public PicsumMetadataProvider()
    {
        _tempFolder = Path.GetTempFileName();
        File.Delete(_tempFolder);
        Directory.CreateDirectory(_tempFolder);
    }
    
    public string Name => "picsum";

    public PictureStats SetMetadata(PictureStats pictureData) => pictureData;

    public string? GetRandomPicturePath()
    {
        throw new NotImplementedException();
    }
}