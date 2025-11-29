using System.Security.Authentication;
using Microsoft.Extensions.Options;
using PicArchiver.Web.Endpoints.Filters;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace PicArchiver.Web.Services.RedisServices;

public class LazyRedis : Lazy<Task<ConnectionMultiplexer>>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<LazyRedis> _logger;
    public const string BasePrefix = "picvoter";
    public const string UserKeyPrefix = BasePrefix + ":users:";
    
    private readonly string _pictureKeyPrefix;
    private readonly RedisConfig _config;

    public bool IsRedisConfigured { get; }

    public LazyRedis(IOptions<RedisConfig> config, IMetadataProvider metadataProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<LazyRedis> logger) : base(() => ConnectionMultiplexer.ConnectAsync(config.Value.RedistHost!), isThreadSafe: true)
    {

        _config = config.Value;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _pictureKeyPrefix = $"{BasePrefix}:pics:{metadataProvider.Name}:";
        IsRedisConfigured = !string.IsNullOrEmpty(config.Value.RedistHost);
    }

    public async ValueTask<IDatabase> GetDatabaseAsync()
    {
        if (!IsValueCreated)
        {
            var configOptions = ConfigurationOptions.Parse(_config.RedistHost);
            configOptions.Password = null;
            _logger.LogInformation("Connecting to Redis host {RedisHost}. Prefix: '{PictureKeyPrefix}'",
                configOptions, _pictureKeyPrefix);
        }
        
        var redis = this.Value.IsCompletedSuccessfully ? this.Value.Result : await this.Value.ConfigureAwait(false);
        return redis.GetDatabase();
    }

    public async ValueTask<IDatabase> GetUserDatabaseAsync(Guid userId)
    {
        if (userId != Guid.Empty)
        {
            return (await this.GetDatabaseAsync()).WithKeyPrefix(UserKeyPrefix + userId + ":");
        }
        
        var db = GetCurrentUserDatabase();
        if (db == null)
        {
            throw new AuthenticationException("No current user found.");
        }

        return db;
    }
    
    public IDatabase? GetCurrentUserDatabase()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext.GetCurrentUserDb() is { } userDb)
        {
            return userDb;
        }
        
        return null;
    }
    
    public IDatabase? GetCurrentPictureDatabase()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.Items.TryGetValue("PictureDb", out var db) == true && db is IDatabase pictureDb)
        {
            return pictureDb;
        }
        
        return null;
    }

    public async ValueTask<IDatabase> GetPictureDatabaseAsync(ulong pictureId)
    {
        if (pictureId == 0)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Items.TryGetValue("PictureDb", out var db) == true && db is IDatabase pictureDb)
            {
                return pictureDb;
            }
        }

        return (await this.GetDatabaseAsync()).WithKeyPrefix(_pictureKeyPrefix + pictureId + ":");
    }

    public async ValueTask<IServer> GetServerAsync()
    {
        var redis = this.Value.IsCompletedSuccessfully ? this.Value.Result : await this.Value.ConfigureAwait(false);
        var servers =  redis.GetServers();
        return servers.First();
    }
    
    public async ValueTask<IEnumerable<IServer>> GetServersAsync()
    {
        var redis = this.Value.IsCompletedSuccessfully ? this.Value.Result : await this.Value.ConfigureAwait(false);
        return redis.GetServers();
    }
}

public class RedisConfig
{
    public string RedistHost { get; set; } = "localhost:6379";
}

public static class RedisDbExtensions
{

}
