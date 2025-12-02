using Dapper;

namespace PicArchiver.Web.Data;

public class DataRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DataRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // --- USER OPERATIONS ---

    public async Task CreateUserAsync(User user)
    {
        const string sql = """
                           INSERT INTO `User` (UserId, UserName, Email) 
                           VALUES (@UserId, @UserName, @Email)
                           """;

        using var db = _connectionFactory.CreateConnection();
        await db.ExecuteAsync(sql, user);
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        const string sql = "SELECT * FROM `User` WHERE UserId = @UserId";

        using var db = _connectionFactory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<User>(sql, new { UserId = userId });
    }

    // --- PICTURE OPERATIONS ---

    public async Task<long> CreatePictureAsync(Picture picture)
    {
        // We use LAST_INSERT_ID() to get the auto-incremented ID back
        const string sql = """
                           INSERT INTO Pictures (FileName, IsDeleted, IsIncoming) 
                           VALUES (@FileName, @IsDeleted, @IsIncoming);
                           SELECT LAST_INSERT_ID();
                           """;

        using var db = _connectionFactory.CreateConnection();
        var newId = await db.QuerySingleAsync<long>(sql, picture);
        picture.PictureId = newId;
        return newId;
    }

    public async Task<IEnumerable<Picture>> GetAllIncomingPicturesAsync()
    {
        const string sql = "SELECT * FROM Pictures WHERE IsIncoming = 1 AND IsDeleted = 0";

        using var db = _connectionFactory.CreateConnection();
        return await db.QueryAsync<Picture>(sql);
    }

    // --- FAVORITES OPERATIONS ---

    public async Task ToggleFavoriteAsync(Guid userId, long pictureId, bool isActive)
    {
        // We use ON DUPLICATE KEY UPDATE to handle both Insert (first time) and Update (toggling)
        const string sql = """
                           INSERT INTO UserFavorites (UserId, PictureId, IsActive, DateTime) 
                           VALUES (@UserId, @PictureId, @IsActive, @DateTime)
                           ON DUPLICATE KEY UPDATE IsActive = @IsActive, DateTime = @DateTime
                           """;

        using var db = _connectionFactory.CreateConnection();
        await db.ExecuteAsync(sql, new { 
            UserId = userId, 
            PictureId = pictureId, 
            IsActive = isActive,
            DateTime = DateTime.UtcNow
        });
    }

    public async Task<IEnumerable<Picture>> GetUserFavoritesAsync(Guid userId)
    {
        // Join to get actual Picture objects that are active favorites
        const string sql = """
                           SELECT p.* 
                           FROM Pictures p
                           INNER JOIN UserFavorites uf ON p.PictureId = uf.PictureId
                           WHERE uf.UserId = @UserId AND uf.IsActive = 1 AND p.IsDeleted = 0
                           """;

        using var db = _connectionFactory.CreateConnection();
        return await db.QueryAsync<Picture>(sql, new { UserId = userId });
    }

    // --- VOTING OPERATIONS ---

    public async Task CastVoteAsync(Guid userId, long pictureId, VoteDirection direction)
    {
        // Convert Enum to lowercase string for MySQL ENUM column
        string voteString = direction.ToString().ToLower(); 

        const string sql = """
                           INSERT INTO PictureVotes (UserId, PictureId, VoteDirection, DateTime) 
                           VALUES (@UserId, @PictureId, @VoteDirection, @DateTime)
                           ON DUPLICATE KEY UPDATE VoteDirection = @VoteDirection, DateTime = @DateTime
                           """;

        using var db = _connectionFactory.CreateConnection();
        await db.ExecuteAsync(sql, new { 
            UserId = userId, 
            PictureId = pictureId, 
            VoteDirection = voteString,
            DateTime = DateTime.UtcNow
        });
    }

    public async Task<int> GetUpvoteCountAsync(long pictureId)
    {
        const string sql = "SELECT COUNT(*) FROM PictureVotes WHERE PictureId = @PictureId AND VoteDirection = 'up'";

        using var db = _connectionFactory.CreateConnection();
        return await db.ExecuteScalarAsync<int>(sql, new { PictureId = pictureId });
    }
}