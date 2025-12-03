using Dapper;
using Microsoft.Extensions.Options;
using PicArchiver.Web.Endpoints.Filters;
using PicArchiver.Web.Services.RedisServices;

namespace PicArchiver.Web.Services.MySqlServices;

public class SqlUserService : IUserService
{
    private readonly IDbConnectionAccessor _connectionAccessor;
    private readonly ILogger<RedisUserService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PictureProvidersConfig _config;

    public SqlUserService(
        IDbConnectionAccessor connectionAccessor,
        IOptions<PictureProvidersConfig> config, ILogger<RedisUserService> logger, 
        IHttpContextAccessor httpContextAccessor)
    {
        _connectionAccessor = connectionAccessor;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _config = config.Value;
    }
    
    public async Task<UserData> AddUser(Guid userId)
    {
        const string sql = """
                           INSERT INTO `User` (UserId, UserName, Email) 
                           VALUES (@UserId, @UserName, @Email)
                           """;
        await _connectionAccessor.DbConnection.ExecuteAsync(sql, 
            new { UserId = userId, UserName = string.Empty, Email = string.Empty });
        
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
        const string sql = "SELECT 1 FROM `User` WHERE `UserId` = @UserId";
        var exists = await _connectionAccessor.DbConnection.ExecuteScalarAsync<int>(sql, 
            new { UserId = userId });
        return exists == 1;
    }

    public async Task<UserData?> GetUserData(Guid userId)
    {
        const string sql = "SELECT UserId Id FROM `User` WHERE `UserId` = @UserId";
        var userData = await _connectionAccessor.DbConnection.QueryFirstOrDefaultAsync<UserData>(sql, 
            new { UserId = userId });
        
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
        const string favsSql = "SELECT PictureId FROM UserFavorites WHERE UserId = @UserId AND IsActive = 1";
        var favs = await _connectionAccessor.DbConnection.QueryAsync<ulong>(favsSql, 
            new { UserId = userId });
        return favs.OrderByDescending(f => f).Select(f => $"{f}").ToList();
    }
}