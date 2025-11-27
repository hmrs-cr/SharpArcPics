using Microsoft.Extensions.Options;

namespace PicArchiver.Web.Services.RedisServices;

public class RedisUserService : IUserService
{
    private readonly LazyRedis redis;
    private readonly string favsHashKey;
    private readonly PictureProvidersConfig config;

    public RedisUserService(LazyRedis redis, IMetadataProvider metadataProvider, IOptions<PictureProvidersConfig> config)
    {
        this.redis = redis;
        this.config = config.Value;
        this.favsHashKey = $"favs:{metadataProvider.Name}";
    }
    
    public async Task<UserData> AddUser(Guid userId)
    {
        var userDb = await this.redis.GetUserDatabaseAsync(userId);
        await userDb.HashSetAsync("attributes", "datetime-created", DateTime.UtcNow.ToString("s"));
        return UserData.Create(userId, this.config);
    }

    public async Task<bool> IsValidUser(Guid userId)
    {
        var userDb = await this.redis.GetUserDatabaseAsync(userId);
        return await userDb.KeyExistsAsync("attributes");
    }

    public async Task<UserData?> GetUserData(Guid userId)
    {
        var userDb = await this.redis.GetUserDatabaseAsync(userId);
        var exists = await userDb.KeyExistsAsync("attributes");
        if (!exists)
        {
            return null;
        }
        
        var allFavorites = await userDb.HashGetAllAsync(this.favsHashKey);
        var favs = allFavorites.OrderByDescending(f => f.Value).Select(f => f.Name.ToString()).ToList();
        var result = UserData.Create(userId, this.config, favs);
        return result;
    }

    public async Task<ICollection<string>> GetUserFavorites(Guid userId)
    {
        var userDb = await this.redis.GetUserDatabaseAsync(userId);
        var allFavorites = await userDb.HashGetAllAsync(this.favsHashKey);
        var result = allFavorites.OrderByDescending(f => f.Value).Select(f => f.Name.ToString()).ToList();
        return result;
    }
}