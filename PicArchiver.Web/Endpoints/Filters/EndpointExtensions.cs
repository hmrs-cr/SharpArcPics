using System.Transactions;
using PicArchiver.Core.DataAccess;

namespace PicArchiver.Web.Endpoints.Filters;

public static class EndpointExtensions
{
    extension(HttpContext? contex)
    {
        public Guid EnsureValidUserSession(Guid requestUserId)
        {
            var user = contex.GetCurrentUser();
            if (requestUserId != Guid.Empty)
            {
                if (requestUserId != contex.GetCurrentUser()?.Id)
                {
                    //throw new InvalidOperationException($"UserId different than logged in user.");
                }

                return requestUserId;
            }

            return user?.Id ?? throw new InvalidOperationException($"No valid user session");
        }

        public UserData? GetCurrentUser() =>
            contex?.Items.TryGetValue("CurrentUserData", out var ud) == true && ud is UserData userData
                ? userData
                : null;

        public UserData SetCurrentUserData(UserData userData)
        {
            contex?.Items["CurrentUserData"] = userData;
            return userData;
        }
    }

    extension<TBuilder>(TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        public TBuilder WithTransaction() => builder.AddEndpointFilter(TransactionFilter);

        public TBuilder AdminUserRequired()
        {
            builder.AddEndpointFilter(ValidRoleFilter.IsAdminUserFilter);
            return builder;
        }
    }

    public static RouteGroupBuilder UserRequired(this RouteGroupBuilder builder) =>
        builder.AddEndpointFilter<ValidUserFilter>();
    
    public static RouteHandlerBuilder UserRequired(this RouteHandlerBuilder builder) =>
        builder.AddEndpointFilter<ValidUserFilter>();

    private static ValueTask<object?> TransactionFilter(EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        using var transactionScope = new TransactionScope();
        var result = next(context);
        transactionScope.Complete();
        return result;
    }
}