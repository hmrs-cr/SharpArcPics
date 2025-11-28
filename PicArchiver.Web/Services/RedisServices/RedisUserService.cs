using Microsoft.Extensions.Options;
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

    public Task<UserData?> CurrentUserData => GetCurrentUserData();
    
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
        if (httpContext?.Items.TryGetValue(nameof(CurrentUserData), out var ud) == true && ud is UserData userData)
        {
            return userData;
        }

        if (httpContext?.Request.Headers.TryGetValue("uid", out var userId) == true &&
            Guid.TryParse(userId, out var userGuid))
        {
            httpContext.Items["UserDb"] =  await this.redis.GetUserDatabaseAsync(userGuid);
            
            var currentUserData = await GetUserData(Guid.Empty);
            currentUserData?.Id = userGuid;
            httpContext.Items[nameof(CurrentUserData)] = currentUserData;
            
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