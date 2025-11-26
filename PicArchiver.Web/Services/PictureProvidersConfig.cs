namespace PicArchiver.Web.Services;

public class PictureProvidersConfig
{
    public string PicturesBasePath { get; init; } = "/media/pictures-data";
    public string? FavsLabel { get; set; }
    public string? TopRatedLabel { get; set; }
    public string? LowRatedLabel { get; set; }
}