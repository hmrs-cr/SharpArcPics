namespace PicArchiver.Web.Services.RedisServices;

public class RedisUserService : IUserService
{
    private readonly LazyRedis redis;

    public RedisUserService(LazyRedis redis)
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
        var result = allFavorites.OrderByDescending(f => f.Value).Select(f => f.Name.ToString()).ToList();
        return result;
    }
}