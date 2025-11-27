using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using PicArchiver.Web.Services;
using PicArchiver.Web.Services.RedisServices;

namespace PicArchiver.Web;

public sealed class WebApp
{
    static WebApp()
    {
        var args = Environment.GetCommandLineArgs();
        var builder = WebApplication.CreateSlimBuilder(args);
        
        builder.Services.AddOpenApi()
            .AddSingleton<IContentTypeProvider, FileExtensionContentTypeProvider>()
            .AddRedisServices(builder.Configuration)
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
        var pictureApi = WebApplication.MapGroup("/picture");
        pictureApi.MapGet("/next", GetRandomPicture).WithName("GetNextPicture");
        pictureApi.MapGet("/{pictureId}", GetPictureById).WithName("GetPicture");
        pictureApi.MapGet("/{pictureId}/thumb", GetPictureThumbnail).WithName("GetPictureThumbnail");
        pictureApi.MapGet("/toprated", GetTopRatedPictures).WithName("GetTopRatedPictures");
        pictureApi.MapGet("/lowrated", GetLowRatedPictures).WithName("GetLowRatedPictures");

        pictureApi.MapPut("/{pictureId}/up", UpvotePicture).WithName("UpvotePicture");
        pictureApi.MapDelete("/{pictureId}/up", UpvotePictureRemove).WithName("UpvotePictureRemove");

        pictureApi.MapPut("/{pictureId}/down", DownvotePicture).WithName("DownvotePicture");
        pictureApi.MapDelete("/{pictureId}/down", DownvotePictureRemove).WithName("DownvotePictureRemove");

        pictureApi.MapPut("/{pictureId}/fav", FavPicture).WithName("FavPicture");
        pictureApi.MapDelete("/{pictureId}/fav", FavPictuReremove).WithName("FavPictuReremove");

        var userApi = WebApplication.MapGroup("/user");
        userApi.MapPost(string.Empty, AddUser).WithName("AddUser");
        userApi.MapGet(string.Empty, GetUser).WithName("GetUser");
        userApi.MapGet("favs", GetMyFavorites).WithName("GetMyFavorites");
    }

    static async Task<IResult> GetTopRatedPictures(IPictureService pictureService)
    {
        var result = await pictureService.GetTopRatedPicturesIds();
        return Results.Ok(result);
    }

    static async Task<IResult> GetLowRatedPictures(IPictureService pictureService)
    {
        var result = await pictureService.GetLowRatedPicturesIds();
        return Results.Ok(result);
    }

    static async Task<IResult> GetMyFavorites(IUserService userService, [FromHeader] Guid uid)
    {
        return Results.Ok(await userService.GetUserFavorites(uid));
    }

    static async Task<IResult> UpvotePicture(IPictureService pictureService, [FromHeader] Guid uid, ulong pictureId) =>
        Results.Ok(await pictureService.Upvote(pictureId, uid));

    static async Task<IResult> UpvotePictureRemove(IPictureService pictureService, [FromHeader] Guid uid,
        ulong pictureId) =>
        Results.Ok(await pictureService.Upvote(pictureId, uid, remove: true));

    static async Task<IResult>
        DownvotePicture(IPictureService pictureService, [FromHeader] Guid uid, ulong pictureId) =>
        Results.Ok(await pictureService.Downvote(pictureId, uid));

    static async Task<IResult> DownvotePictureRemove(IPictureService pictureService, [FromHeader] Guid uid,
        ulong pictureId) =>
        Results.Ok(await pictureService.Downvote(pictureId, uid, remove: true));

    static async Task<IResult> FavPicture(IUserService userService, IPictureService pictureService,
        [FromHeader] Guid uid, ulong pictureId)
    {
        await pictureService.Favorite(pictureId, uid);
        return Results.Ok(await userService.GetUserFavorites(uid));
    }

    static async Task<IResult> FavPictuReremove(IUserService userService, IPictureService pictureService,
        [FromHeader] Guid uid, ulong pictureId)
    {
        await pictureService.Favorite(pictureId, uid, remove: true);
        return Results.Ok(await userService.GetUserFavorites(uid));
    }

    static async Task<IResult> GetUser(IUserService userService, [FromHeader] Guid uid)
    {
        if (await userService.GetUserData(uid) is { } userData)
        {
            return Results.Ok(userData);
        }

        return Results.NotFound();
    }

    static async Task<IResult> AddUser(IUserService userService)
    {
        var newUser = await userService.AddUser();
        return Results.Ok(newUser);
    }

    static async Task<IResult> GetPictureThumbnail(IContentTypeProvider contentTypeProvider,
        IPictureService pictureService, ulong pictureId)
    {
        var path = await pictureService.GetPictureThumbPath(pictureId);
        if (path == null)
        {
            return Results.NotFound();
        }

        var ext = Path.GetExtension(path);
        var contentType = contentTypeProvider.TryGetContentType(ext, out var mimeType) ? mimeType : null;
        return Results.File(path, contentType: contentType, fileDownloadName: $"{pictureId}-thumb{ext}",
            enableRangeProcessing: true);
    }

    static Task<IResult> GetPictureById(IPictureService pictureService, [FromHeader] Guid uid, long token,
        ulong pictureId, HttpContext context) => GetPicture(pictureService, uid, token, pictureId, context);

    static Task<IResult> GetRandomPicture(IPictureService pictureService, [FromHeader] Guid uid, long token,
        HttpContext context) => GetPicture(pictureService, uid, token, null, context);

    static async Task<IResult> GetPicture(IPictureService pictureService, [FromHeader] Guid uid, long token,
        ulong? pictureId, HttpContext context)
    {
        _ = uid;
        _ = token;

        var pictureData = pictureId.HasValue
            ? await pictureService.GetPictureData(pictureId.Value, uid)
            : await pictureService.GetRandomPictureData(uid);
        if (pictureData == null)
        {
            return Results.NotFound();
        }


        foreach (var metadata in pictureData.Metadata)
        {
            context.Response.Headers[metadata.Key] = metadata.Value;
        }

        context.Response.Headers.Append("InternalId", pictureData.PictureId.ToString());
        context.Response.Headers.Append("IsFav", pictureData.Favs.ToString());
        context.Response.Headers.Append("Upvoted", pictureData.UpVotes.ToString());
        context.Response.Headers.Append("Downvoted", pictureData.DownVotes.ToString());
        context.Response.Headers.Append("SourceUrl", pictureData.SourceUrl);

        _ = pictureService.IncrementPictureView(pictureData.PictureId, uid);

        return Results.File(pictureData.FullFilePath, contentType: pictureData.MimeType,
            fileDownloadName: pictureData.DownloadName, enableRangeProcessing: true);
    }
}