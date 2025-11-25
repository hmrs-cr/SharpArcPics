namespace PicArchiver.Web.Services;

public interface IUserService
{
    Task<bool> AddUser(Guid userId);
    Task<bool> IsValidUser(Guid userId);

    Task<ICollection<string>> GetUserFavorites(Guid userId);
}