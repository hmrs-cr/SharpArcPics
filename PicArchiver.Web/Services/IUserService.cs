namespace PicArchiver.Web.Services;

public interface IUserService
{
    Task<UserData> AddUser(Guid userId);
    Task<bool> IsValidUser(Guid userId);
    
    Task<UserData?> GetUserData(Guid userId);

    Task<ICollection<string>> GetUserFavorites(Guid userId);
}

public class UserData
{
    public Guid Id { get; init; }
    public string? FavsLabel { get; init; }
    public string? TopRatedLabel { get; init; }
    public string? LowRatedLabel { get; init; }
    public ICollection<string> Favs { get; init; }
}