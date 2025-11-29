namespace PicArchiver.Web.Endpoints.Filters;

public class ValidRoleFilter : IEndpointFilter
{
    private readonly IEnumerable<string> _roles;
    
    public static ValidRoleFilter IsAdminUserFilter { get; } = new("Admin");
    
    public ValidRoleFilter(params IEnumerable<string> roles)
    {
        _roles = roles;
    }
    
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var currentUser = context.HttpContext.GetCurrentUser();
        if (currentUser?.HasRoles(_roles) == true)
        {
            return await next(context);
        }
        
        return Results.Unauthorized();
    }
}