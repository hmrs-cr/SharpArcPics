using System.Diagnostics;
using PicArchiver.Web.Services.Ig;
using PicArchiver.Web.Services.Picsum;

namespace PicArchiver.Web.Services;

public static class RegistrationExtensions
{
    public static IServiceCollection AddMetadataProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("PictureProviders:Default");
        if (provider is null)
        {
            throw new  ApplicationException(nameof(provider));
        }
        
        var registrationFunc = GetRegistrationFunction(provider.ToLower());
        if (registrationFunc is null)
        {
            throw new ApplicationException($"No provider registered for type {provider}");
        }
        
        return registrationFunc(services, configuration);;
    }

    private static Func<IServiceCollection, IConfiguration, IServiceCollection>
        GetRegistrationFunction(string provider) => provider switch
    {
        "ig" => IgRegistrationExtensions.AddIgMetadataProvider,
        "picsum" => PicsumRegistrationExtensions.AddPicsumMetadataProvider,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
    };
}