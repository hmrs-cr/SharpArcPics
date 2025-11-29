namespace PicArchiver.Web.Services.Ig;

public static class IgRegistrationExtensions
{
    public static IServiceCollection AddIgMetadataProvider(this IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton<IMetadataProvider, IgMetadataProvider>()
                .AddSingleton<IPictureProvider, IgPicturePool>()
                .Configure<PictureProvidersConfig>( configuration.GetSection("PictureProviders:Ig"));
}