using Microsoft.Extensions.Options;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;

namespace PicArchiver.Web.Services.RedisServices;

public class LazyRedis : Lazy<Task<ConnectionMultiplexer>>
{
    public const string BasePrefix = "picvoter";
    public const string UserKeyPrefix = BasePrefix + ":users:";
    
    private readonly string _pictureKeyPrefix;
    
    public bool IsRedisConfigured { get; }

    public LazyRedis(IOptions<RedisConfig> config, IMetadataProvider metadataProvider) : base(() => ConnectionMultiplexer.ConnectAsync(config.Value.RedistHost!), isThreadSafe: true)
    {
        _pictureKeyPrefix = $"{BasePrefix}:pics:{metadataProvider.Name}:";
        IsRedisConfigured = !string.IsNullOrEmpty(config.Value.RedistHost);
    }

    public async ValueTask<IDatabase> GetDatabaseAsync()
    {
        var redis = this.Value.IsCompletedSuccessfully ? this.Value.Result : await this.Value.ConfigureAwait(false);
        return redis.GetDatabase();
    }

    public async ValueTask<IDatabase> GetUserDatabaseAsync(Guid userId)
    {
        var db = await this.GetDatabaseAsync();
        return db.WithKeyPrefix(UserKeyPrefix + userId + ":");
    }
    
    public async ValueTask<IDatabase> GetPictureDatabaseAsync(ulong pictureId)
    {
        var db = await this.GetDatabaseAsync();
        return db.WithKeyPrefix(_pictureKeyPrefix + pictureId + ":");
    }

    public async ValueTask<IServer> GetServerAsync()
    {
        var redis = this.Value.IsCompletedSuccessfully ? this.Value.Result : await this.Value.ConfigureAwait(false);
        var servers =  redis.GetServers();
        return servers.First();
    }
}

public class RedisConfig
{
    public string RedistHost { get; set; } = "localhost:6379";
}
