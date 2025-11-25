using System.Collections.Concurrent;
using Microsoft.AspNetCore.StaticFiles;
using PicArchiver.Core.Metadata;
using PicArchiver.Extensions;

namespace PicArchiver.Web.Services.RedisServices;

public class RedisPictureService : IPictureService
{   
    private readonly ConcurrentDictionary<string, int> toprated = new();
    private readonly ConcurrentDictionary<string, int> lowrated = new();

    private readonly IMetadataProvider metadataProvider;
    private readonly IRandomProvider randomProvider;
    private readonly LazyRedis redis;
    private readonly IContentTypeProvider contentTypeProvider;
    private readonly ILogger<RedisPictureService> logger;

    public RedisPictureService(
        IMetadataProvider metadataProvider,
        IRandomProvider randomProvider,
        LazyRedis redis, 
        IContentTypeProvider contentTypeProvider,
        ILogger<RedisPictureService> logger)
    {
        this.metadataProvider = metadataProvider;
        this.randomProvider = randomProvider;
        this.redis = redis;
        this.contentTypeProvider = contentTypeProvider;
        this.logger = logger;
    }
    
    public async Task<PictureStats?> GetRandomPictureData(Guid requestUserId)
    {
        _ = requestUserId;

        var maxRetries = 10;
        while (maxRetries-- > 0)
        {
            var fullPicturePath = await this.randomProvider.GetNextRandomValueAsync();
            if (File.Exists(fullPicturePath))
            {
                var pictureId = fullPicturePath.ComputeHash();
                var result = await GetPictureData(pictureId, requestUserId);
                if (result != null)
                {
                    if (result.Views > 0)
                    {
                        this.logger.LogInformation("User {UID} already viewed picture {PID}. Selecting another picture [{c}].", requestUserId, pictureId, maxRetries);
                        continue;
                    }
                    
                    return result;
                }

                return await SavePicturePath(pictureId, fullPicturePath);
            }
        }

        return null;
    }

    public async Task<string?> GetPictureThumbPath(ulong pictureId)
    {
        var pictureDb = await this.redis.GetPictureDatabaseAsync(pictureId);
        var path = await pictureDb.HashGetAsync("attributes", "path");
        return path;
    }
    private async Task UpdateVoteCont()
    {
        var db = await this.redis.GetDatabaseAsync();
        var server = await this.redis.GetServerAsync();
        var votes = new Dictionary<string, int>();
        await foreach(var key in server.KeysAsync(pattern: LazyRedis.UserKeyPrefix + "*:votes"))
        {
            var allvotes = await db.HashGetAllAsync(key);
            foreach (var vote in allvotes)
            {
                if (vote.Value.StartsWith("up|"))
                {
                    var id = vote.Name.ToString();
                    votes[id] = votes.GetValueOrDefault(id) + 1;
                }
                else if (vote.Value.StartsWith("down|"))
                {
                    var id = vote.Name.ToString();
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
    }
    
    public async Task<ICollection<string>> GetTopRatedPicturesIds()
    {
        await UpdateVoteCont();
        return toprated.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).ToList();
    }
    
    public async Task<ICollection<string>> GetLowRatedPicturesIds()
    {
        await UpdateVoteCont();
        return lowrated.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
    }

    public async Task<PictureStats?> GetPictureData(ulong pictureId, Guid requestUserId)
    {
        var pictureDb = await this.redis.GetPictureDatabaseAsync(pictureId);
        var path = await pictureDb.HashGetAsync("attributes", "path");
        if (path.HasValue)
        {
            var result = new PictureStats(path.ToString());
            if (requestUserId != Guid.Empty)
            {
                var pictureKey = $"{pictureId}";
                var userDb = await this.redis.GetUserDatabaseAsync(requestUserId);
                
                var views = await userDb.HashGetAsync("views", pictureKey);
                var isFav = await userDb.HashExistsAsync("favs", pictureKey);
                var votes = await userDb.HashGetAsync("votes", pictureKey);
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
            await userDb.HashDeleteAsync("favs", pictureKey);
        }
        else
        {
            await userDb.HashSetAsync("favs", pictureKey, $"{DateTime.UtcNow:s}");
        }

        return 1;
    }

    private async Task<int> Vote(ulong pictureId, Guid requestUserId, bool isUp, bool remove)
    {
        var pictureKey = $"{pictureId}";
        var userDb = await this.redis.GetUserDatabaseAsync(requestUserId);

        if (remove)
        {
            await userDb.HashDeleteAsync("votes", pictureKey);
        }
        else
        {
            var vote = isUp ? "up" : "down";
            await userDb.HashSetAsync("votes", pictureKey, $"{vote}|{DateTime.UtcNow:s}");
        }

        return 1;
    }

    public async Task<PictureStats> SavePicturePath(ulong pictureId, string picturePath)
    {
        var pictureDb = await this.redis.GetPictureDatabaseAsync(pictureId);
        await pictureDb.HashSetAsync("attributes", "path", picturePath);
        return this.SetMetadatada(new PictureStats(picturePath));
    }
    
    public async Task<int> IncrementPictureView(ulong pictureId, Guid requestUserId)
    {
        var pictureKey = $"{pictureId}";
        var userDb = await this.redis.GetUserDatabaseAsync(requestUserId);
        
        var views = await userDb.HashGetAsync("views", pictureKey);
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
        
        await userDb.HashSetAsync("views", pictureKey, $"{DateTime.UtcNow:s}|{viewCount}");
        return viewCount;
    }

    private PictureStats SetMetadatada(PictureStats pictureStats)
    {
        pictureStats.MimeType = this.GetContentType(pictureStats.Ext);
        return this.metadataProvider.SetMetadata(pictureStats);
    }
}