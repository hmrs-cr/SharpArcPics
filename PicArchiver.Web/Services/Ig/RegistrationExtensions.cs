namespace PicArchiver.Web.Services.Ig;

public static class RegistrationExtensions
{
    public static IServiceCollection AddIgMetadataProvider(this IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton<IMetadataProvider, IgMetadataProvider>()
                .AddSingleton<IRandomProvider, IgRandomPool>()
                .Configure<IgMetadataConfig>(configuration);
}