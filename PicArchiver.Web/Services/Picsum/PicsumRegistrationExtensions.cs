namespace PicArchiver.Web.Services.Picsum;

public static class PicsumRegistrationExtensions
{
    public static IServiceCollection AddPicsumMetadataProvider(this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddSingleton<IMetadataProvider, PicsumMetadataProvider>()
            .AddSingleton<IRandomProvider, PicsumRamdomProvider>()
            .Configure<PictureProvidersConfig>( configuration.GetSection("PictureProviders:Picsum"))
            .AddHttpClient();
}