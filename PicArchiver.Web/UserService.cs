using System.Collections.Concurrent;
using PicArchiver.Web.Data;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace PicArchiver.Web;

public interface IUserService
{
    Task<bool> AddUser(Guid userId);
    Task<bool> IsValidUser(Guid userId);

    Task<ICollection<string>> GetUserFavorites(Guid userId);
}

public class UserService : IUserService
{
    private readonly LazyRedis redis;

    public UserService(LazyRedis redis)
    {
        this.redis = redis;
    }
    
    public async Task<bool> AddUser(Guid userId)
    {
        var userDb = await this.redis.GetUserDatabaseAsync(userId);
        await userDb.HashSetAsync("attributes", "datetime-created", DateTime.UtcNow.ToString("s"));
        return true;
    }

    public async Task<bool> IsValidUser(Guid userId)
    {
        var userDb = await this.redis.GetUserDatabaseAsync(userId);
        return await userDb.KeyExistsAsync("attributes");
    }

    public async Task<ICollection<string>> GetUserFavorites(Guid userId)
    {
        var userDb = await this.redis.GetUserDatabaseAsync(userId);
        var allFavorites = await userDb.HashGetAllAsync("favs");
        var result = allFavorites.OrderByDescending(f => f.Value).Select(f => f.Key.ToString()).ToList();
        return result;
    }
}