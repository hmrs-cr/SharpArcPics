using Dapper;
using Microsoft.Extensions.Options;
using PicArchiver.Web.Endpoints.Filters;

namespace PicArchiver.Web.Services.MySqlServices;

public class SqlUserService : IUserService
{
    private readonly IDbConnectionAccessor _connectionAccessor;
    private readonly ILogger<SqlUserService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PictureProvidersConfig _config;

    public SqlUserService(
        IDbConnectionAccessor connectionAccessor,
        IOptions<PictureProvidersConfig> config, 
        ILogger<SqlUserService> logger, 
        IHttpContextAccessor httpContextAccessor)
    {
        _connectionAccessor = connectionAccessor;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _config = config.Value;
    }
    
    public async Task<UserData> AddUser(Guid userId)
    {
        await _connectionAccessor.DbConnection.AddUser(userId);
        _logger.LogInformation("New User with Id {userId} created.", userId);
        return UserData.Create(userId, this._config);
    }

    public Task<UserData> AddUser()
    {
        var uid = Guid.NewGuid();
        return this.AddUser(uid);
    }

    public async Task<bool> IsValidUser(Guid userId)
    {
        var exists = await _connectionAccessor.DbConnection.IsValidUser(userId);
        return exists == 1;
    }

    public async Task<UserData?> GetUserData(Guid userId)
    {
        var userData = await _connectionAccessor.DbConnection.GetUserData(userId);
        if (userData == null)
        {
            return null;
        }
        
        var userFavorites = await this.GetUserFavorites(userId);
        return UserData.Create(userId, this._config, userFavorites);
    }

    public async Task<UserData?> GetCurrentUserData()
    {
        var httpContext = this._httpContextAccessor.HttpContext;
        if (httpContext.GetCurrentUser() is { } userData)
        {
            return userData;
        }

        if (httpContext?.Request.Headers.TryGetValue("uid", out var userId) == true &&
            Guid.TryParse(userId, out var userGuid))
        {
            var currentUserData = await GetUserData(userGuid);
            if (currentUserData != null)
            {
                httpContext.SetCurrentUserData(currentUserData);
            }
            
            return currentUserData;
        }
            
        return null;
    }

    public async Task<ICollection<string>> GetUserFavorites(Guid userId)
    {
        userId = _httpContextAccessor.HttpContext.EnsureValidUserSession(userId);
        var favs = await _connectionAccessor.DbConnection.GetUserFavorites(userId);
        return favs.OrderByDescending(f => f).Select(f => $"{f}").ToList();
    }
}