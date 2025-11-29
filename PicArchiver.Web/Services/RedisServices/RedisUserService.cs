using Microsoft.Extensions.Options;
using PicArchiver.Web.Endpoints.Filters;
using StackExchange.Redis;

namespace PicArchiver.Web.Services.RedisServices;

public class RedisUserService : IUserService
{
    private readonly LazyRedis redis;
    private readonly ILogger<RedisUserService> logger;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly string favsHashKey;
    private readonly PictureProvidersConfig config;

    public RedisUserService(LazyRedis redis, IMetadataProvider metadataProvider, 
        IOptions<PictureProvidersConfig> config, ILogger<RedisUserService> logger, 
        IHttpContextAccessor httpContextAccessor)
    {
        this.redis = redis;
        this.logger = logger;
        this.httpContextAccessor = httpContextAccessor;
        this.config = config.Value;
        this.favsHashKey = $"favs:{metadataProvider.Name}";
    }
    
    public async Task<UserData> AddUser(Guid userId)
    {
        var userDb = await this.redis.GetUserDatabaseAsync(userId);
        await userDb.HashSetAsync("attributes", "datetime-created", DateTime.UtcNow.ToString("s"));
        this.logger.LogInformation("New User with Id {userId} created.", userId);
        return UserData.Create(userId, this.config);
    }

    public Task<UserData> AddUser()
    {
        var uid = Guid.NewGuid();
        return this.AddUser(uid);
    }

    public async Task<bool> IsValidUser(Guid userId)
    {
        var userDb =  await this.redis.GetUserDatabaseAsync(userId);
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

    public async Task<UserData?> GetCurrentUserData()
    {
        var httpContext = this.httpContextAccessor.HttpContext;
        if (httpContext.GetCurrentUser() is { } userData)
        {
            return userData;
        }

        if (httpContext?.Request.Headers.TryGetValue("uid", out var userId) == true &&
            Guid.TryParse(userId, out var userGuid))
        {
            httpContext.SetCurrentUserDb(await this.redis.GetUserDatabaseAsync(userGuid));
            var currentUserData = await GetUserData(Guid.Empty);
            if (currentUserData != null)
            {
                currentUserData.Id = userGuid;
                httpContext.SetCurrentUserData(currentUserData);
            }
            
            return currentUserData;
        }
            
        return null;
    }

    public async Task<ICollection<string>> GetUserFavorites(Guid userId)
    {
        var userDb = await this.redis.GetUserDatabaseAsync(userId);
        var allFavorites = await userDb.HashGetAllAsync(this.favsHashKey);
        var result = allFavorites.OrderByDescending(f => f.Value).Select(f => f.Name.ToString()).ToList();
        return result;
    }
}