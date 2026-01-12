using Microsoft.AspNetCore.StaticFiles;
using PicArchiver.Web.Endpoints.Filters;
using PicArchiver.Web.Services;

namespace PicArchiver.Web.Endpoints;

internal static class PictureEndpoints
{
    public static IEndpointRouteBuilder AddPictureEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var pictureApi = routeBuilder.MapGroup("/picture").UserRequired();
        
        pictureApi.MapGet("/next", GetRandomPicture).WithName("GetNextPicture")
            .WithDescription("Gets the next random picture data with basic metadata in the headers");
        
        pictureApi.MapGet("/{pictureId}", GetPictureById).WithName("GetPicture")
            .WithDescription("Gets picture data by id with basic metadata in the headers");
        
        pictureApi.MapGet("/{pictureId}/metadata", GetPictureMetadataById).WithName("GetPictureMetadata")
            .WithDescription("Returns detailed picture metadata of the specified picture");
        
        pictureApi.MapDelete("/{pictureId}", DeletePictureById).WithName("DeletePictureById").AdminUserRequired()
            .WithDescription("Marks a picture deleted by id");
        
        pictureApi.MapGet("/{pictureId}/thumb", GetPictureThumbnail).WithName("GetPictureThumbnail")
            .WithDescription("Gets the thumbnail data of the picture");
        
        pictureApi.MapGet("/set/top-rated", GetTopRatedPictures).WithName("GetTopRatedPictures")
            .WithDescription("Returns the top-rated pictures ids");
        
        pictureApi.MapGet("/set/low-rated", GetLowRatedPictures).WithName("GetLowRatedPictures")
            .WithDescription("Returns the low-rated pictures ids");
        
        pictureApi.MapGet("/set/my-favs", GetMyFavorites).WithName("GetMyFavoritesSet")
            .WithDescription("Returns current user favorite pictures ids");
        
        pictureApi.MapGet("/set/{setId}", GetImageSet).WithName("GetImageSet")
            .WithDescription("Returns the picture ids of the specified image set. 'setId' Could be an album id, an user name, a custom album name, etc");

        pictureApi.MapPut("/{pictureId}/up", UpvotePicture).WithName("UpvotePicture")
            .WithDescription("Upvotes a picture");
        
        pictureApi.MapDelete("/{pictureId}/up", UpvotePictureRemove).WithName("UpvotePictureRemove")
            .WithDescription("Removes upvote for a picture");

        pictureApi.MapPut("/{pictureId}/down", DownvotePicture).WithName("DownvotePicture")
            .WithDescription("Downvotes a picture");
        
        pictureApi.MapDelete("/{pictureId}/down", DownvotePictureRemove).WithName("DownvotePictureRemove")
            .WithDescription("Removes downvote for a picture");

        pictureApi.MapPut("/{pictureId}/fav", FavPicture).WithName("FavPicture")
            .WithDescription("Adds a favorite picture to the favorite set of the current user");
        
        pictureApi.MapDelete("/{pictureId}/fav", FavPictuReremove).WithName("FavPictureRemove")
            .WithDescription("Removes favorite picture from the favorite set of the current user");
        
        pictureApi.MapGet("/search", SearchPictures).WithName("SearchPictures")
            .WithDescription("Search the pictures using the specified text as search criteria");

        return routeBuilder;
    }
    
    private static Task<ICollection<string>> GetMyFavorites(IUserService userService) =>
        userService.GetUserFavorites(Guid.Empty); 
    
    private static Task<ICollection<string>> GetImageSet(IPictureService pictureService, string setId) => 
        pictureService.GetImageSet(setId);
    
    private static Task<ICollection<string>> GetTopRatedPictures(IPictureService pictureService) =>
        pictureService.GetTopRatedPicturesIds();

    private static Task<ICollection<string>> GetLowRatedPictures(IPictureService pictureService) =>
        pictureService.GetLowRatedPicturesIds();

    private static Task<int> UpvotePicture(IPictureService pictureService, ulong pictureId) =>
        pictureService.Upvote(pictureId, Guid.Empty);

    private static Task<int> UpvotePictureRemove(IPictureService pictureService,
        ulong pictureId) =>
        pictureService.Upvote(pictureId, Guid.Empty, remove: true);

    private static Task<int>
        DownvotePicture(IPictureService pictureService, ulong pictureId) =>
        pictureService.Downvote(pictureId, Guid.Empty);

    private static Task<int> DownvotePictureRemove(IPictureService pictureService,
        ulong pictureId) =>
        pictureService.Downvote(pictureId, Guid.Empty, remove: true);

    private static async Task<ICollection<string>> FavPicture(IUserService userService, IPictureService pictureService,
        ulong pictureId)
    {
        await pictureService.Favorite(pictureId, Guid.Empty);
        return await userService.GetUserFavorites(Guid.Empty);
    }

    private static async Task<ICollection<string>> FavPictuReremove(IUserService userService, IPictureService pictureService,
        ulong pictureId)
    {
        await pictureService.Favorite(pictureId, Guid.Empty, remove: true);
        return await userService.GetUserFavorites(Guid.Empty);
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

    private static Task<IResult> SearchPictures(IPictureService pictureService, string q)
    {
        // TODO: implement
        throw  new NotImplementedException();
    }
    
    private static Task<IResult> GetPictureMetadataById(IPictureService pictureService, long token,
        ulong pictureId, HttpContext context) => // TODO: Implement
                                                 GetPicture(pictureService, token, pictureId, context);
    
    private static Task<IResult> GetPictureById(IPictureService pictureService, long token,
        ulong pictureId, HttpContext context) => GetPicture(pictureService, token, pictureId, context);

    private static async Task<IResult> DeletePictureById(IPictureService pictureService, long token,
        ulong pictureId) =>  await pictureService.DeletePicture(pictureId) ? Results.Ok() : Results.BadRequest();

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

        await pictureService.IncrementPictureView(pictureData.PictureId, Guid.Empty);

        return Results.File(pictureData.FullFilePath, contentType: pictureData.MimeType,
            fileDownloadName: pictureData.DownloadName, enableRangeProcessing: true);
    }
}