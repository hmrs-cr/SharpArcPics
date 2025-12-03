using System.Data;
using MySql.Data.MySqlClient;

namespace PicArchiver.Web.Services.MySqlServices;

public interface IDbConnectionAccessor
{
    public IDbConnection DbConnection { get; }
}

public class HttpContextMySqlConnectionAccessor : IDbConnectionAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _connectionString;

    public HttpContextMySqlConnectionAccessor(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PicVoterMySql") ?? 
                            throw new InvalidOperationException("No MySQL Connection configured");
        
        _httpContextAccessor = httpContextAccessor;
    }

    public IDbConnection DbConnection => GetDbConnection();

    private IDbConnection GetDbConnection()
    {
        var dbConnection = _httpContextAccessor.HttpContext?.RequestServices.GetRequiredService<IDbConnection>() ??
            throw new InvalidOperationException("Not in a HttpContext");
        
        if (dbConnection.State == ConnectionState.Closed)
        {
            dbConnection.ConnectionString = _connectionString;
        }
        
        return dbConnection;
    }
}

public static class RegistrationExtensions
{
    public static IServiceCollection AddMySql(this IServiceCollection services) =>
        services.AddSingleton<IDbConnectionAccessor, HttpContextMySqlConnectionAccessor>()
                .AddSingleton<IUserService, SqlUserService>()
                .AddScoped<IDbConnection, MySqlConnection>();
}