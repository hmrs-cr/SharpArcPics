namespace PicArchiver.Web.Services.Picsum;

public static class RegistrationExtensions
{
    public static IServiceCollection AddPicsumMetadataProvider(this IServiceCollection services,
        IConfiguration configuration) =>
        services.AddSingleton<IMetadataProvider, PicsumMetadataProvider>()
            .AddSingleton<IRandomProvider, PicsumRamdomProvider>()
            .AddHttpClient();
}