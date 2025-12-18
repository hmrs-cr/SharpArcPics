using System.Data;
using System.Diagnostics;
using Dapper;
using PicArchiver.Core.Metadata.Loaders;

namespace PicArchiver.Core.DataAccess;

public static class DbConnectionPictureExtensions
{
    extension(IDbConnection connection)
    {
        public async Task<string?> GetPicturePath(ulong pictureId)
        {
            const string sql = "SELECT FileName FROM Pictures WHERE PictureID = @PictureId AND IsDeleted = 0";

            return await connection.QueryFirstOrDefaultAsync<string>(sql,
                new { PictureId = pictureId });
        }
        
        public async Task<int> AddPicturePath(ulong pictureId, string path)
        {
            const string sql = """
                               INSERT IGNORE INTO Pictures  (PictureId, FileName, IsDeleted, IsIncoming) 
                               VALUES (@PictureId, @FileName, 0, 0)
                               """;

            return await connection.ExecuteAsync(sql, new { PictureId = pictureId, FileName = Path.GetFileName(path) });
        }

        public async Task<int> AddOrUpdatePictureIgGraphMetadata(ulong pictureId, IgFile igFile, IgGraphNode metadata, bool setIncomingFlag)
        {
            const string insertSql = """
                               INSERT IGNORE INTO Pictures
                                    (PictureId,
                                     FileName,
                                     IsIncoming,
                                     IsDeleted)
                               VALUES (@PictureId,
                                       @FileName,
                                       @IsIncoming,
                                       0);
                               INSERT INTO IgMetadata
                                    (IgUserId,
                                     IgPictureId,
                                     IgPostId,
                                     PictureId,
                                     TakenAt,
                                     Caption,
                                     ShortCode)
                               VALUES 
                                   (@IgUserId,
                                    @IgPictureId,
                                    @IgPostId,
                                    @PictureId,
                                    @TakenAt,
                                    @Caption,
                                    @ShortCode)
                               ON DUPLICATE KEY UPDATE
                                    PictureId =  @PictureId,
                                    IgPostId = @IgPostId,
                                    TakenAt = @TakenAt,
                                    Caption = @Caption,
                                    ShortCode = @ShortCode;
                               INSERT INTO IgUserNames
                                    (IgUserId, 
                                     IgUserName, 
                                     IgFullName)
                               VALUES
                                   (@IgUserId,
                                    @IgUserName,
                                    @IgFullName)
                               ON DUPLICATE KEY UPDATE
                                    IgFullName = @IgFullName;
                               """;

            Debug.Assert(igFile.UserId ==  metadata.IgOwner.Id, "Ig UserId does not match");

            var isCarrouselPost = metadata.CarrouselCount > 1;
            var saveDetails = !isCarrouselPost || metadata.CurrentCarrouselIndex == 1;
            return await connection.ExecuteAsync(insertSql, new
            {
                IgUserId = igFile.UserId,
                IgPictureId = igFile.PictureId,
                IgUserName = igFile.UserName,
                IgFullName = metadata.IgOwner.FullName,
                IgPostId = isCarrouselPost ? metadata.Id : null,
                PictureId = pictureId,
                TakenAt = igFile.Timestamp > 0 ? igFile.Timestamp : (long?)null,
                Caption = saveDetails ? metadata.Caption : null,
                ShortCode = saveDetails ? metadata.Shortcode : null,
                FileName = Path.GetFileName(igFile.FileName),
                IsIncoming = setIncomingFlag,
            });
        }
        
        public async Task<int> AddOrUpdatePictureMetadata(ulong pictureId, string path, 
            IgMetadataRoot.MetadataData metadata, string? igUserName, long? igUserId, long? igPicId,
            DateTime? dateAdded, bool setIncomingFlag)
        {
            const string sql = """
                               INSERT INTO Pictures  
                                   (PictureId,
                                    IgUserId,
                                    IgPictureId,
                                    FileName,
                                    Description,
                                    Clothing,
                                    Emotions,
                                    Objects,
                                    People,
                                    Race,
                                    Gender,
                                    DateAdded,
                                    IsIncoming,
                                    IsDeleted) 
                               VALUES (@PictureId, 
                                       @IgUserId,
                                       @IgPictureId,
                                       @FileName,
                                       @Description,
                                       @Clothing,
                                       @Emotions,
                                       @Objects,
                                       @People,
                                       @Race,
                                       @Gender,
                                       @DateAdded,
                                       @IsIncoming, 
                                       0) 
                               ON DUPLICATE KEY UPDATE
                                       FileName = @FileName,
                                       Description  = @Description,
                                       Clothing  = @Clothing,
                                       Emotions  = @Emotions,
                                       Objects   = @Objects,
                                       People    = @People,
                                       Race      = @Race,
                                       Gender    = @Gender,
                                       IsIncoming = @IsIncoming,
                                       DateAdded = @DateAdded;
                                INSERT IGNORE INTO IgUserNames 
                                    (IgUserId, IgUserName) 
                                VALUES 
                                    (@IgUserId, @IgUserName);
                               """;

            return await connection.ExecuteAsync(sql, new
            {
                PictureId = pictureId, 
                FileName = Path.GetFileName(path),
                Description = metadata.Paras?.FirstOrDefault(),
                Clothing = metadata.Table?.Clothing,
                Emotions = metadata.Table?.Emotions,
                Objects = metadata.Table?.Objects,
                People = metadata.Table?.People,   
                Race = metadata.Table?.Race,
                Gender = metadata.Paras?.InferGender(),
                DateAdded = dateAdded,
                IgPictureId = igPicId,
                IgUserId = igUserId,
                IgUserName = igUserName,
                IsIncoming = setIncomingFlag,
            });
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

        public async Task<IEnumerable<ulong>> GetMostVotedPictures(string direction)
        {
            const string sqlBase = """
                               SELECT PictureId, SUM(IF(VoteDirection = 'down', -1, 1)) AS Votes FROM PictureVotes
                               GROUP BY PictureId
                               ORDER BY Votes
                               """;

            const string sqlDown = sqlBase + " LIMIT 100";
            const string sqlUp = sqlBase + " DESC LIMIT 100";

            return await connection.QueryAsync<ulong>(direction == "up" ? sqlUp : sqlDown);
        }
    }
}