using System.Data;
using MySql.Data.MySqlClient;

namespace PicArchiver.Web.Services.MySqlServices;

public interface IDbConnectionAccessor
{
    public IDbConnection DbConnection { get; }
}

public class BasicMySqlConnectionAccessor : IDbConnectionAccessor
{
    private readonly IServiceProvider? _serviceProvider;
    private readonly string _connectionString;
    
    public BasicMySqlConnectionAccessor(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("PicVoterMySql") ?? 
                            throw new InvalidOperationException("No MySQL Connection configured");
    }
    
    public BasicMySqlConnectionAccessor(IServiceProvider serviceProvider, IConfiguration configuration) : this(configuration)
    {
        _serviceProvider = serviceProvider;
    }
    
    public virtual IDbConnection DbConnection => GetDbConnection(_serviceProvider ?? throw new InvalidOperationException("Not in a DbContext"));
    
    protected IDbConnection GetDbConnection(IServiceProvider serviceProvider)
    {
        var dbConnection = serviceProvider.GetRequiredService<IDbConnection>();
        if (dbConnection.State == ConnectionState.Closed)
        {
            dbConnection.ConnectionString = _connectionString;
        }
        
        return dbConnection;
    }
}

public class HttpContextMySqlConnectionAccessor : BasicMySqlConnectionAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextMySqlConnectionAccessor(IHttpContextAccessor httpContextAccessor, IConfiguration configuration) :
        base(configuration)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override IDbConnection DbConnection => 
        GetDbConnection(_httpContextAccessor.HttpContext?.RequestServices ?? throw new InvalidOperationException("Not in a HttpContext"));
}

public static class RegistrationExtensions
{
    public static IServiceCollection AddMySql(this IServiceCollection services) =>
        services.AddSingleton<IDbConnectionAccessor, HttpContextMySqlConnectionAccessor>()
                .AddSingleton<IUserService, SqlUserService>()
                .AddSingleton<IPictureService, SqlPictureService>()
                .AddScoped<IDbConnection, MySqlConnection>();
}