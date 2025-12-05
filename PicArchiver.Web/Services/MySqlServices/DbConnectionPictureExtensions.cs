using System.Data;
using Dapper;

namespace PicArchiver.Web.Services.MySqlServices;

public static class DbConnectionPictureExtensions
{
    extension(IDbConnection connection)
    {
        public async Task<string?> GetPicturePath(ulong pictureId)
        {
            const string sql = "SELECT FileName FROM Pictures WHERE PictureID = @PictureId AND IsDeleted != 0";

            return await connection.QueryFirstOrDefaultAsync<string>(sql,
                new { PictureId = pictureId });
        }
        
        public async Task<int> AddPicturePath(ulong pictureId, string path)
        {
            const string sql = """
                               INSERT INTO Pictures  (PictureId, FileName, IsDeleted, IsIncoming) 
                               VALUES (@PictureId, @FileName, 0, 0)
                               """;

            return await connection.ExecuteAsync(sql, new { PictureId = pictureId, FileName = path });
        }
        
        public async Task<int> AddPictureView(Guid userId, ulong pictureId)
        {
            const string sql = """
                               INSERT INTO PictureViews (UserId, PictureId) 
                               VALUES (@UserId, @PictureId)
                               """;

            return await connection.ExecuteAsync(sql, new { UserId = userId, PictureId = pictureId });
        }
        
        public async Task<int> DeletePicture(ulong pictureId)
        {
            const string sql = "UPDATE Pictures p SET p.IsDeleted=1 WHERE PictureId = @PictureId";
            return await connection.ExecuteAsync(sql, new { PictureId = pictureId });
        }

        public async Task<long> GetPictureViewCount(Guid? userId = null, ulong? pictureId = null)
        {
            const string sqlAll = "SELECT COUNT(1) FROM PictureViews";
            const string sqlUser = sqlAll + " WHERE UserID = @UserId";
            const string sqlPicture = sqlAll + " WHERE PictureId = @PictureId";
            const string sqlBoth = sqlAll + " WHERE PictureId = @PictureId AND UserID = @UserId";

            var sql = sqlAll;
            object? arguments = null;
            if (userId.HasValue && pictureId.HasValue)
            {
                sql = sqlBoth;
                arguments = new { UserId = userId, PictureId = pictureId };
            } 
            else if (userId.HasValue)
            {
                sql = sqlUser;
                arguments = new { UserId = userId };
            }
            else if (pictureId.HasValue)
            {
                sql = sqlPicture;
                arguments = new { PictureId = pictureId };
            }
            
            return await connection.ExecuteScalarAsync<long>(sql, arguments);
        }

        public async Task<long> GetPictureFavoriteCount(Guid? userId = null, ulong? pictureId = null)
        {
            const string sqlAll = "SELECT COUNT(1) FROM UserFavorites";
            const string sqlUser = sqlAll + " WHERE UserID = @UserId";
            const string sqlPicture = sqlAll + " WHERE PictureId = @PictureId";
            const string sqlBoth = sqlAll + " WHERE PictureId = @PictureId AND UserID = @UserId";

            var sql = sqlAll;
            object? arguments = null;
            if (userId.HasValue && pictureId.HasValue)
            {
                sql = sqlBoth;
                arguments = new { UserId = userId, PictureId = pictureId };
            } 
            else if (userId.HasValue)
            {
                sql = sqlUser;
                arguments = new { UserId = userId };
            }
            else if (pictureId.HasValue)
            {
                sql = sqlPicture;
                arguments = new { PictureId = pictureId };
            }
            
            return await connection.ExecuteScalarAsync<long>(sql, arguments);

        }

        public async Task<string?> GetVote(Guid userId, ulong pictureId)
        {
            const string sql = "SELECT VoteDirection FROM PictureVotes WHERE UserID = @UserId AND PictureId  = @PictureId";
            return await connection.ExecuteScalarAsync<string>(sql, new { UserId = userId, PictureId = pictureId });
        }

        public async Task<int> MarkPicturesAsFavorite(Guid? userId, ulong? pictureId, bool remove = false)
        {
            const string sql = """
                               INSERT INTO UserFavorites (UserId, PictureId, IsActive)
                                    VALUES (@UserId, @PictureId, @IsActive)
                               ON DUPLICATE KEY UPDATE
                                    IsActive = @IsActive,
                                    DateTime = CURRENT_TIMESTAMP
                               """;
            
            return await connection.ExecuteAsync(sql, new { PictureId = pictureId, UserId = userId, IsActive = remove ? 0 : 1 });
        }
        
        public async Task<int> VoteForPicture(Guid? userId, ulong? pictureId, string direction, bool remove = false)
        {
            const string sql = """
                               INSERT INTO PictureVotes (UserId, PictureId, VoteDirection, IsActive)
                                    VALUES (@UserId, @PictureId, @VoteDirection, @IsActive)
                               ON DUPLICATE KEY UPDATE
                                    IsActive = @IsActive,
                                    DateTime = CURRENT_TIMESTAMP
                               """;
            
            return await connection.ExecuteAsync(sql, new 
                { PictureId = pictureId, UserId = userId, VoteDirection = direction, IsActive = remove ? 0 : 1 });
        }
    }
}