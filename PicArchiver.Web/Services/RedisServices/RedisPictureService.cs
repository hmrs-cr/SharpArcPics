using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.StaticFiles;
using PicArchiver.Core.Metadata;
using PicArchiver.Extensions;
using StackExchange.Redis;

namespace PicArchiver.Web.Services.RedisServices;

public class RedisPictureService : IPictureService
{   
    private static readonly TimeSpan MaxUpdateCountTimespan = TimeSpan.FromMinutes(10);
    
    private readonly ConcurrentDictionary<string, int> toprated = new();
    private readonly ConcurrentDictionary<string, int> lowrated = new();

    private readonly IMetadataProvider metadataProvider;
    private readonly IPictureProvider _pictureProvider;
    private readonly LazyRedis redis;
    private readonly IContentTypeProvider contentTypeProvider;
    private readonly ILogger<RedisPictureService> logger;

    private readonly string picSetName;
    private readonly string votesHashKey;
    private readonly string favsHashKey;
    private readonly string viewsHashKey;

    private DateTimeOffset? _lastUpdateVoteCountTime;

    public RedisPictureService(
        IMetadataProvider metadataProvider,
        IPictureProvider pictureProvider,
        LazyRedis redis, 
        IContentTypeProvider contentTypeProvider,
        ILogger<RedisPictureService> logger)
    {
        this.metadataProvider = metadataProvider;
        this._pictureProvider = pictureProvider;
        this.redis = redis;
        this.contentTypeProvider = contentTypeProvider;
        this.logger = logger;
        
        this.picSetName = metadataProvider.Name;
        this.votesHashKey = $"votes:{this.picSetName}";
        this.favsHashKey = $"favs:{this.picSetName}";
        this.viewsHashKey = $"views:{this.picSetName}";
    }
    
    public async Task<PictureStats?> GetRandomPictureData(Guid requestUserId)
    {
        _ = requestUserId;

        var maxRetries = 10;
        while (maxRetries-- > 0)
        {
            var pictureContextData = await this._pictureProvider.GetNextRandomValueAsync();
            var fullPicturePath = pictureContextData.Key;
            var pictureId = fullPicturePath.ComputeHash();
            var result = await GetPictureData(pictureId, requestUserId, true);
            if (result != null)
            {
                if (result.Views > 0)
                {
                    if (this.logger.IsEnabled(LogLevel.Debug))
                        this.logger.LogDebug(
                            "User {UID} already viewed picture {PID}. Selecting another picture [{c}].", requestUserId,
                            pictureId, maxRetries);
                    continue;
                }

                result.ContextData = pictureContextData.Value;
                return result;
            }

            return SavePicturePath(pictureId, fullPicturePath, pictureContextData.Value);
        }

        return null;
    }

    public async Task<string?> GetPictureThumbPath(ulong pictureId)
    {
        var pictureDb = await this.redis.GetPictureDatabaseAsync(pictureId);
        var path = await pictureDb.HashGetAsync("attributes", "path");
        return path;
    }
    
    private int _isVoteCountUpdating;
    
    private async Task UpdateVoteCont(bool force)
    {
        if (Interlocked.CompareExchange(ref _isVoteCountUpdating, 1, 0) == 0)
        {
            try
            {
                if (_lastUpdateVoteCountTime.HasValue && DateTimeOffset.UtcNow - _lastUpdateVoteCountTime.Value <
                    MaxUpdateCountTimespan)
                {
                    return;
                }
                
                if ((!toprated.IsEmpty || !lowrated.IsEmpty) && !force)
                {
                    return;
                }

                var startTimestamp = DateTimeOffset.UtcNow;
                logger.LogInformation("Starting to update vote count");
                
                var db = await this.redis.GetDatabaseAsync();
                var server = await this.redis.GetServerAsync();
                var votes = new Dictionary<string, int>();
                await foreach (var key in server.KeysAsync(pattern: LazyRedis.UserKeyPrefix + $"*:{this.votesHashKey}"))
                {
                    var allvotes = await db.HashGetAllAsync(key);
                    foreach (var vote in allvotes)
                    {
                        var id = vote.Name.ToString();
                        var pictureDb = await this.redis.GetPictureDatabaseAsync(ulong.Parse(id));
                        var path = await pictureDb.HashGetAsync("attributes", "path");
                        if (!path.HasValue || !File.Exists(path))
                        {
                            _ = db.HashDeleteAsync(key, vote.Name);
                            continue;
                        }

                        if (vote.Value.StartsWith("up|"))
                        {
                            votes[id] = votes.GetValueOrDefault(id) + 1;
                        }
                        else if (vote.Value.StartsWith("down|"))
                        {
                            votes[id] = votes.GetValueOrDefault(id) - 1;
                        }
                    }
                }

                toprated.Clear();
                foreach (var vote in votes.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Take(100))
                {
                    toprated.TryAdd(vote.Key, vote.Value);
                }

                lowrated.Clear();
                foreach (var vote in votes.Where(kv => kv.Value < 0).OrderBy(kv => kv.Value).Take(100))
                {
                    lowrated.TryAdd(vote.Key, vote.Value);
                }

#if DEBUG
                _lastUpdateVoteCountTime = DateTimeOffset.MinValue;
#else                
                _lastUpdateVoteCountTime = DateTimeOffset.UtcNow;
#endif                
                
                logger.LogInformation("Finished to updating vote count. Total time: {time}", _lastUpdateVoteCountTime - startTimestamp);
            }
            finally
            {
                Interlocked.Exchange(ref _isVoteCountUpdating, 0);
            }
        }
    }
    
    public async Task<ICollection<string>> GetTopRatedPicturesIds()
    {
        await UpdateVoteCont(false);
        return toprated.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
    }
    
    public async Task<ICollection<string>> GetLowRatedPicturesIds()
    {
        await UpdateVoteCont(false);
        return lowrated.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
    }

    public async Task<ICollection<string>> GetImageSet(string setId)
    {
        var picSet = ulong.TryParse(setId, out var setIdl) ? this._pictureProvider.GetPictureSetPaths(setIdl) : _pictureProvider.GetPictureSetPaths(setId);
        var result = new List<string>(32);
        await foreach (var path in picSet.OrderByDescending(p => p))
        {
            var pictureId = path.ComputeHash();
            await SavePicToDbAsync(pictureId, path);
            result.Add($"{pictureId}");
        }

        return result;
    }

    public Task<ICollection<string>> GetDeletedIds()
    {
        return Task.FromResult<ICollection<string>>([]);
    }

    public Task<ICollection<string>> GetIncomingIds()
    {
        return Task.FromResult<ICollection<string>>([]);
    }

    public async Task<bool> DeletePicture(ulong pictureId)
    {
        var pictureDb = await this.redis.GetPictureDatabaseAsync(pictureId);
        var path = await pictureDb.HashGetAsync("attributes", "path");
        if (path.HasValue)
        {
            try
            {
                File.Delete(path.ToString());
                if (File.Exists(path))
                {
                    logger.LogWarning("Failed to delete picture {PictureId}: '{path}'", pictureId, path);
                    return false;
                }
                
                var servers = await this.redis.GetServersAsync();
                var keysToDelete = new List<RedisKey>();
                foreach (var server in servers)
                {
                    await foreach (var key in server.KeysAsync(pattern: $"{LazyRedis.BasePrefix}:*:{pictureId}:*"))
                    {
                        keysToDelete.Add(key);
                    }
                }
                
                if (keysToDelete.Count > 0)
                {
                    var db = await this.redis.GetDatabaseAsync();
                    await db.KeyDeleteAsync(keysToDelete.ToArray());
                }
                
                _lastUpdateVoteCountTime = DateTimeOffset.MinValue;
                _ = UpdateVoteCont(true);
                
                return true;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to delete picture {PictureId}: '{path}'", pictureId, path);
            }
        }
        
        return false;
    }

    public async Task<PictureStats?> GetPictureData(ulong pictureId, Guid? requestUserId, bool onlyIfNotViewed = false)
    {
        var pictureDb = await this.redis.GetPictureDatabaseAsync(pictureId);
        var path = await pictureDb.HashGetAsync("attributes", "path");
        if (path.HasValue && File.Exists(path))
        {
            var result = new PictureStats(path.ToString());
            if (requestUserId.HasValue)
            {
                var pictureKey = $"{pictureId}";
                var userDb = await this.redis.GetUserDatabaseAsync(requestUserId.Value);
                var views = await userDb.HashGetAsync(this.viewsHashKey, pictureKey);
                if (views.HasValue && onlyIfNotViewed)
                {
                    result.Views = 1;
                    return result;
                }

                if (!views.HasValue)
                {
                    // My First view
                    return this.SetMetadatada(result);
                }
                
                var isFavTask = userDb.HashExistsAsync(this.favsHashKey, pictureKey);
                var votesTask = userDb.HashGetAsync(this.votesHashKey, pictureKey);
                
                await Task.WhenAll(isFavTask, votesTask);
                var isFav = isFavTask.Result;
                var votes = votesTask.Result;
                
                var isDowvoted = votes.HasValue && votes.StartsWith("down|");
                var isUpvoted = votes.HasValue && votes.StartsWith("up|");
                
                result.Favs = Convert.ToUInt32(isFav);
                result.UpVotes = Convert.ToUInt32(isUpvoted);
                result.DownVotes = Convert.ToUInt32(isDowvoted);
                result.Views = Convert.ToUInt32(views.HasValue);
            }
            
            return this.SetMetadatada(result);
        }
        
        return null;
    }

    private string? GetContentType(string ext) => 
        this.contentTypeProvider.TryGetContentType(ext, out var contentType) ? contentType : null;

    public Task<int> Upvote(ulong pictureId, Guid requestUserId, bool remove = false)=> 
        this.Vote(pictureId, requestUserId, true, remove);
    
    public Task<int> Downvote(ulong pictureId, Guid requestUserId, bool remove = false) => 
        this.Vote(pictureId, requestUserId, false, remove);

    public async Task<int> Favorite(ulong pictureId, Guid requestUserId, bool remove = false)
    {
        var pictureKey = $"{pictureId}";
        var userDb = await this.redis.GetUserDatabaseAsync(requestUserId);

        if (remove)
        {
            await userDb.HashDeleteAsync(this.favsHashKey, pictureKey);
        }
        else
        {
            await userDb.HashSetAsync(this.favsHashKey, pictureKey, $"{DateTime.UtcNow:s}");
        }

        return 1;
    }

    private async Task<int> Vote(ulong pictureId, Guid requestUserId, bool isUp, bool remove)
    {
        var pictureKey = $"{pictureId}";
        var userDb = await this.redis.GetUserDatabaseAsync(requestUserId);

        if (remove)
        {
            await userDb.HashDeleteAsync(this.votesHashKey, pictureKey);
        }
        else
        {
            var vote = isUp ? "up" : "down";
            await userDb.HashSetAsync(this.votesHashKey, pictureKey, $"{vote}|{DateTime.UtcNow:s}");
        }
        
        _ = UpdateVoteCont(true);

        return 1;
    }

    private PictureStats SavePicturePath(ulong pictureId, string picturePath, object? contextData = null)
    {
        _ = SavePicToDbAsync(pictureId, picturePath);
        return this.SetMetadatada(new PictureStats(picturePath) { ContextData  = contextData});
    }
    
    private async Task SavePicToDbAsync(ulong picId, string path, IDatabase? database = null)
    {
        var pictureDb = database ?? await this.redis.GetPictureDatabaseAsync(picId);
        var existingPath = await pictureDb.HashGetAsync("attributes", "path");
        if (existingPath.HasValue && existingPath.ToString() != path)
        {
            logger.LogWarning("Id collision detected for {id}: '{existingPath}' and {path}", picId, existingPath, path);
        }
        
        if (!existingPath.HasValue)
        {
            await pictureDb.HashSetAsync("attributes", "path", path);
        }
    }
    
    public async Task<int> IncrementPictureView(ulong pictureId, Guid requestUserId)
    {
        var pictureKey = $"{pictureId}";
        var userDb = await this.redis.GetUserDatabaseAsync(requestUserId);
        
        var views = await userDb.HashGetAsync(this.viewsHashKey, pictureKey);
        var viewCount = 1;
        if (views.HasValue)
        {
            var viewsValue = views.ToString();
            var i = viewsValue.LastIndexOf('|');
            if (i > 0)
            {
                viewCount = int.Parse(viewsValue.AsSpan(i + 1));
                viewCount++;
            }
        }
        
        await userDb.HashSetAsync(this.viewsHashKey, pictureKey, $"{DateTime.UtcNow:s}|{viewCount}");
        return viewCount;
    }

    private PictureStats SetMetadatada(PictureStats pictureStats)
    {
        pictureStats.MimeType ??= this.GetContentType(pictureStats.Ext);
        return pictureStats.Metadata.Count == 0 ? this.metadataProvider.SetMetadata(pictureStats) : pictureStats;
    }
}