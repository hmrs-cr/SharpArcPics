using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using PicArchiver.Web.Endpoints.Filters;
using PicArchiver.Web.Services;

namespace PicArchiver.Web.Endpoints;

internal static class PictureEndpoints
{
    public static IEndpointRouteBuilder AddPictureEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var pictureApi = routeBuilder.MapGroup("/picture").AddEndpointFilter<ValidUserFilter>();
        
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

        return routeBuilder;
    }
    
    private static async Task<IResult> GetTopRatedPictures(IPictureService pictureService)
    {
        var result = await pictureService.GetTopRatedPicturesIds();
        return Results.Ok(result);
    }

    private static async Task<IResult> GetLowRatedPictures(IPictureService pictureService)
    {
        var result = await pictureService.GetLowRatedPicturesIds();
        return Results.Ok(result);
    }

    private static async Task<IResult> UpvotePicture(IPictureService pictureService, ulong pictureId) =>
        Results.Ok(await pictureService.Upvote(pictureId, Guid.Empty));

    private static async Task<IResult> UpvotePictureRemove(IPictureService pictureService,
        ulong pictureId) =>
        Results.Ok(await pictureService.Upvote(pictureId, Guid.Empty, remove: true));

    private static async Task<IResult>
        DownvotePicture(IPictureService pictureService, ulong pictureId) =>
        Results.Ok(await pictureService.Downvote(pictureId, Guid.Empty));

    private static async Task<IResult> DownvotePictureRemove(IPictureService pictureService,
        ulong pictureId) =>
        Results.Ok(await pictureService.Downvote(pictureId, Guid.Empty, remove: true));

    private static async Task<IResult> FavPicture(IUserService userService, IPictureService pictureService,
        ulong pictureId)
    {
        await pictureService.Favorite(pictureId, Guid.Empty);
        return Results.Ok(await userService.GetUserFavorites(Guid.Empty));
    }

    private static async Task<IResult> FavPictuReremove(IUserService userService, IPictureService pictureService,
        ulong pictureId)
    {
        await pictureService.Favorite(pictureId, Guid.Empty, remove: true);
        return Results.Ok(await userService.GetUserFavorites(Guid.Empty));
    }
    
    private static async Task<IResult> GetPictureThumbnail(IContentTypeProvider contentTypeProvider,
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

    private static Task<IResult> GetPictureById(IPictureService pictureService, long token,
        ulong pictureId, HttpContext context) => GetPicture(pictureService, token, pictureId, context);

    private static Task<IResult> GetRandomPicture(IPictureService pictureService, long token,
        HttpContext context) => GetPicture(pictureService, token, null, context);
    
    private static async Task<IResult> GetPicture(IPictureService pictureService, long token,
        ulong? pictureId, HttpContext context)
    {
        _ = token;

        var pictureData = pictureId.HasValue
            ? await pictureService.GetPictureData(pictureId.Value, Guid.Empty)
            : await pictureService.GetRandomPictureData(Guid.Empty);
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

        _ = pictureService.IncrementPictureView(pictureData.PictureId, Guid.Empty);

        return Results.File(pictureData.FullFilePath, contentType: pictureData.MimeType,
            fileDownloadName: pictureData.DownloadName, enableRangeProcessing: true);
    }
}