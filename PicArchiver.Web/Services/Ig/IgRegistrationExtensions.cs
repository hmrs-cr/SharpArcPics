namespace PicArchiver.Web.Services.Ig;

public static class IgRegistrationExtensions
{
    public static IServiceCollection AddIgMetadataProvider(this IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton<IMetadataProvider, IgMetadataProvider>()
                .AddSingleton<IRandomProvider, IgRandomPool>()
                .Configure<PictureProvidersConfig>( configuration.GetSection("PictureProviders:Ig"));
}