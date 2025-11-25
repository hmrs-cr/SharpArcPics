namespace PicArchiver.Web.Services.RedisServices;

public static class RegistrationExtensions
{
    public static IServiceCollection AddRedisServices(this IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton<IPictureService, RedisPictureService>()
                .AddSingleton<IUserService, RedisUserService>()
                .AddSingleton<IRandomProvider, RandomPool>()
                .AddSingleton<LazyRedis>()
                .Configure<RedisConfig>(configuration);
}