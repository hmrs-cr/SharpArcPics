using Microsoft.AspNetCore.StaticFiles;
using PicArchiver.Core.DataAccess;
using PicArchiver.Core.Metadata;
using PicArchiver.Web.Endpoints.Filters;

namespace PicArchiver.Web.Services.MySqlServices;

public class SqlPictureService : IPictureService
{   
    private readonly IMetadataProvider _metadataProvider;
    private readonly IPictureProvider _pictureProvider;
    private readonly IContentTypeProvider _contentTypeProvider;
    private readonly ILogger<SqlPictureService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDbConnectionAccessor _connectionAccessor;

    public SqlPictureService(
        IMetadataProvider metadataProvider,
        IPictureProvider pictureProvider,
        IContentTypeProvider contentTypeProvider,
        ILogger<SqlPictureService> logger,
        IHttpContextAccessor httpContextAccessor,
        IDbConnectionAccessor connectionAccessor)
    {
        _metadataProvider = metadataProvider;
        _pictureProvider = pictureProvider;
        _contentTypeProvider = contentTypeProvider;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _connectionAccessor = connectionAccessor;
    }
    
    public async Task<PictureStats?> GetRandomPictureData(Guid requestUserId)
    {
        requestUserId = _httpContextAccessor.HttpContext.EnsureValidUserSession(requestUserId);
        var maxRetries = 10;
        while (maxRetries-- > 0)
        {
            var fullPicturePath = await this._pictureProvider.GetNextRandomValueAsync();
            var pictureId = _pictureProvider.GetPictureIdFromPath(fullPicturePath);;
            var result = await GetPictureData(pictureId, requestUserId, true);
            if (result != null)
            {
                if (result.Views > 0)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug(
                            "User {UID} already viewed picture {PID}. Selecting another picture [{c}].", requestUserId,
                            pictureId, maxRetries);
                    continue;
                }
                
                return result;
            }

            return await SavePicturePath(pictureId, fullPicturePath);
        }

        return null;
    }

    public async Task<string?> GetPictureThumbPath(ulong pictureId)
    {
        var picPath = await _connectionAccessor.DbConnection.GetPicturePath(pictureId);
        return _pictureProvider.CreatePictureStats(picPath, pictureId)?.FullFilePath;
    }
    
    public async Task<ICollection<string>> GetTopRatedPicturesIds()
    {
        var result = await _connectionAccessor.DbConnection.GetMostVotedPictures("up");
        return result.Select(f => $"{f}").ToList();
    }
    
    public async Task<ICollection<string>> GetLowRatedPicturesIds()
    {
        var result = await _connectionAccessor.DbConnection.GetMostVotedPictures("down");
        return result.Select(f => $"{f}").ToList();
    }

    public async Task<ICollection<string>> GetImageSet(string setId)
    {
        var picSet = ulong.TryParse(setId, out var setIdl) ? this._pictureProvider.GetPictureSetPaths(setIdl) : _pictureProvider.GetPictureSetPaths(setId);
        var result = new List<string>(32);
        await foreach (var path in picSet.OrderByDescending(p => p))
        {
            var pictureId = _pictureProvider.GetPictureIdFromPath(path);
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
        var result = await _connectionAccessor.DbConnection.DeletePicture(pictureId);
        return result == 1;
    }

    public async Task<PictureStats?> GetPictureData(ulong pictureId, Guid? requestUserId, bool onlyIfNotViewed = false)
    {
        var path = await _connectionAccessor.DbConnection.GetPicturePath(pictureId);
        var result = _pictureProvider.CreatePictureStats(path, pictureId);
        if (result != null)
        {
            if (requestUserId.HasValue)
            {
                requestUserId = _httpContextAccessor.HttpContext.EnsureValidUserSession(requestUserId.Value);
                var views = await _connectionAccessor.DbConnection.GetPictureViewCount(userId: requestUserId, pictureId: pictureId);
                if (views > 0 && onlyIfNotViewed)
                {      
                    result.Views = 1;
                    return result;
                }

                if (views == 0)
                {
                    // My First view
                    return await this.SetMetadatada(result);
                }
                
                var voteDirection = await _connectionAccessor.DbConnection.GetVote(requestUserId.Value, pictureId);
                var isDowvoted = voteDirection == "down";
                var isUpvoted = voteDirection == "up";
                
                result.Favs = await _connectionAccessor.DbConnection.GetPictureFavoriteCount(pictureId: pictureId, userId: requestUserId);
                result.UpVotes = Convert.ToUInt32(isUpvoted);
                result.DownVotes = Convert.ToUInt32(isDowvoted);
                result.Views = views;
            }
            
            return await this.SetMetadatada(result);
        }
        
        return null;
    }

    private string? GetContentType(string ext) => 
        this._contentTypeProvider.TryGetContentType(ext, out var contentType) ? contentType : null;

    public Task<int> Upvote(ulong pictureId, Guid requestUserId, bool remove = false)=> 
        this.Vote(pictureId, requestUserId, true, remove);
    
    public Task<int> Downvote(ulong pictureId, Guid requestUserId, bool remove = false) => 
        this.Vote(pictureId, requestUserId, false, remove);

    public async Task<int> Favorite(ulong pictureId, Guid requestUserId, bool remove = false)
    {
        requestUserId =  _httpContextAccessor.HttpContext.EnsureValidUserSession(requestUserId);
        var result = await _connectionAccessor.DbConnection.MarkPicturesAsFavorite(
            userId: requestUserId, pictureId: pictureId, remove);
        return result;
    }

    private async Task<int> Vote(ulong pictureId, Guid requestUserId, bool isUp, bool remove)
    {
        var vote = isUp ? "up" : "down";
        requestUserId = _httpContextAccessor.HttpContext.EnsureValidUserSession(requestUserId);
        var result = await _connectionAccessor.DbConnection.VoteForPicture(requestUserId, pictureId, vote, remove);
        return result;
    }

    private async Task<PictureStats> SavePicturePath(ulong pictureId, string picturePath)
    {
        await SavePicToDbAsync(pictureId, picturePath);
        return await this.SetMetadatada(_pictureProvider.CreatePictureStats(picturePath, pictureId)!);
    }

    private async Task SavePicToDbAsync(ulong picId, string path)
    {
        await _connectionAccessor.DbConnection.AddPicturePath(picId, path);
    }

    public async Task<int> IncrementPictureView(ulong pictureId, Guid requestUserId)
    {
        requestUserId = _httpContextAccessor.HttpContext.EnsureValidUserSession(requestUserId);
        var result = await _connectionAccessor.DbConnection.AddPictureView(requestUserId, pictureId);
        return result;
    }

    private async ValueTask<PictureStats> SetMetadatada(PictureStats pictureStats)
    {
        pictureStats.MimeType ??= this.GetContentType(pictureStats.Ext);
        return pictureStats.Metadata.Count == 0 ? await this._metadataProvider.SetMetadata(pictureStats) : pictureStats;
    }
}