using System.Reflection;
using Microsoft.AspNetCore.StaticFiles;
using PicArchiver.Web.Endpoints;
using PicArchiver.Web.Services;
using PicArchiver.Web.Services.MySqlServices;

namespace PicArchiver.Web;

public sealed class WebApp
{
    static WebApp()
    {
        var args = Environment.GetCommandLineArgs();
        var builder = WebApplication.CreateSlimBuilder(args);
        
        builder.Services.AddOpenApi().AddHttpContextAccessor()
            .AddSingleton<IContentTypeProvider, FileExtensionContentTypeProvider>()
            .AddMySql()
            .AddMetadataProvider(builder.Configuration);
        
        WebApplication = builder.Build();
        
        WebApplication.UseDefaultFiles();
        WebApplication.UseHttpsRedirection();
        WebApplication.UseStaticFiles();
        
        if (WebApplication.Environment.IsDevelopment())
        {
            WebApplication.MapOpenApi();
        }

        Logger = WebApplication.Services.GetRequiredService<ILogger<WebApp>>();
        
        var appVersion = Assembly.GetEntryAssembly()?.GetName().Version;
        Version = appVersion?.ToString() ?? "Unknown";

        var appConfigSection = WebApplication.Configuration.GetSection("Application");
        Name = appConfigSection.GetValue<string>("Name") ?? "Pic Voter";
        Developer = appConfigSection.GetValue<string>("Developer") ?? "HM Soft";
        Description = appConfigSection.GetValue<string>("Description") ?? "Browse, vote, and collect high-quality photography." ;
    }

    public static ILogger<WebApp> Logger { get; }
    public static WebApplication WebApplication { get; }
    
    public static string Version { get; }
    public static string Name { get; }
    public static string Developer { get; }
    public static string Description { get; }

    public static int Run()
    {
        try
        {
            AddEndPoints();
            WebApplication.Run();
            return 0;
        }
        catch (Exception e)
        {
            Logger.LogCritical(e, "Unhandled exception");
            return -1;
        }
    }

    private static void AddEndPoints()
    {
        WebApplication.AddPictureEndpoints()
                      .AddUserEndpoints();
    }
}