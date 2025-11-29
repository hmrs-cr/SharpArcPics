namespace PicArchiver.Web.Services;

public interface IUserService
{
    Task<UserData> AddUser(Guid userId);
    Task<UserData> AddUser();
    Task<bool> IsValidUser(Guid userId);
    
    Task<UserData?> GetUserData(Guid userId);
    
    Task<UserData?> GetCurrentUserData();

    Task<ICollection<string>> GetUserFavorites(Guid userId);
}

public class UserData
{
    public Guid Id { get; set; }
    public ICollection<string>? Favs { get; init; }
    
    public required AppInfo AppInfo { get; init; }
    
    public bool IsAdmin => HasRoles("Admin");

    public static UserData Create(Guid userId, PictureProvidersConfig config, ICollection<string>? favs = null) => new()
    {
        Id = userId,
        Favs = favs,
        AppInfo = new AppInfo
        {
            AppVersion = WebApp.Version,
            AppName = WebApp.Name,
            Developer = WebApp.Developer,
            AppDescription = WebApp.Description,
            FavsLabel = config.FavsLabel,
            LowRatedLabel = config.LowRatedLabel,
            TopRatedLabel = config.TopRatedLabel
        }
    };

    public bool HasRoles(params IEnumerable<string> roles)
    {
        // No roles implement for now.
        // TODO: Implement roles.
        return true;
    }
}

public class AppInfo
{
    public required string AppName { get; init; }
    public required string AppVersion { get; init; }
    public required string AppDescription { get; init; }
    public required string Developer { get; init; }
    
    public string? FavsLabel { get; init; }
    public string? TopRatedLabel { get; init; }
    public string? LowRatedLabel { get; init; }
}