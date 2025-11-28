using Microsoft.AspNetCore.Mvc;
using PicArchiver.Web.Endpoints.Filters;
using PicArchiver.Web.Services;

namespace PicArchiver.Web.Endpoints;

internal static class UserEndpoints
{
    public static IEndpointRouteBuilder AddUserEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var userApi = routeBuilder.MapGroup("/user");
        userApi.MapPost(string.Empty, AddUser).WithName("AddUser");
        userApi.MapGet(string.Empty, GetUser).WithName("GetUser").AddEndpointFilter<ValidUserFilter>();
        userApi.MapGet("favs", GetMyFavorites).WithName("GetMyFavorites").AddEndpointFilter<ValidUserFilter>();

        return routeBuilder;
    }
    
    private static async Task<IResult> GetMyFavorites(IUserService userService)
    {
        return Results.Ok(await userService.GetUserFavorites(Guid.Empty));
    }
    
    
    private static async Task<IResult> GetUser(IUserService userService)
    {
        if (await userService.GetCurrentUserData() is { } userData)
        {
            return Results.Ok(userData);
        }

        return Results.NotFound();
    }

    private static async Task<IResult> AddUser(IUserService userService)
    {
        var newUser = await userService.AddUser();
        return Results.Ok(newUser);
    }
}