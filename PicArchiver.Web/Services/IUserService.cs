using PicArchiver.Core.DataAccess;

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

public static class UserServiceExtensions
{
    public static UserData SetData(this UserData userData, PictureProvidersConfig config, ICollection<string>? favs = null)
    {
        userData.Favs = favs;
        userData.AppInfo = new AppInfo
        {
            AppVersion = WebApp.Version,
            AppName = WebApp.Name,
            Developer = WebApp.Developer,
            AppDescription = WebApp.Description,
            FavsLabel = config.FavsLabel,
            LowRatedLabel = config.LowRatedLabel,
            TopRatedLabel = config.TopRatedLabel
        };
        
        return userData;
    }
}