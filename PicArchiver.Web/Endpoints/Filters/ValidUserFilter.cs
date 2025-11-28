using PicArchiver.Web.Services;

namespace PicArchiver.Web.Endpoints.Filters;

public class ValidUserFilter : IEndpointFilter
{
    private readonly IUserService _userService;

    public ValidUserFilter(IUserService userService)
    {
        _userService = userService;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var currentUserData = await _userService.GetCurrentUserData();
        if (currentUserData == null)
        {
            return Results.Unauthorized();
        }
        
        return await next(context);
    }
}