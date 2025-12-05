using System.Data;
using Dapper;

namespace PicArchiver.Web.Services.MySqlServices;

public static class DbConnectionExtensions
{
    extension(IDbConnection connection)
    {
        public async Task<int> AddUser(Guid userId)
        {
            const string sql = """
                               INSERT INTO `User` (UserId, UserName, Email) 
                               VALUES (@UserId, @UserName, @Email)
                               """;
            return await connection.ExecuteAsync(sql, 
                new { UserId = userId, UserName = string.Empty, Email = string.Empty });
        }

        public async Task<int> IsValidUser(Guid userId)
        {
            const string sql = "SELECT 1 FROM `User` WHERE `UserId` = @UserId";
        
            return await connection.ExecuteScalarAsync<int>(sql, 
                new { UserId = userId });
        }

        public async Task<UserData?> GetUserData(Guid userId)
        {
            const string sql = "SELECT UserId Id FROM `User` WHERE `UserId` = @UserId";
            return await connection.QueryFirstOrDefaultAsync<UserData>(sql, 
                new { UserId = userId });
        }
        
        public async Task<IEnumerable<ulong>> GetUserFavorites(Guid userId)
        {
            const string favsSql = "SELECT PictureId FROM UserFavorites WHERE UserId = @UserId AND IsActive = 1";
            
            return await connection.QueryAsync<ulong>(favsSql, 
                new { UserId = userId });
        }
    }
}