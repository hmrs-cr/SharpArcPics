using PicArchiver.Web.Services;
using StackExchange.Redis;

namespace PicArchiver.Web.Endpoints.Filters;

public static class EndpointExtensions
{
    extension(HttpContext? contex)
    {
        public Guid EnsureValidUserSession(Guid requestUserId)
        {
            if (requestUserId != Guid.Empty)
            {
                return requestUserId;
            }
            
            var user = contex.GetCurrentUser();
            return user?.Id ?? throw new InvalidOperationException($"No valid user session");
        }
        
        public UserData? GetCurrentUser() =>
            contex?.Items.TryGetValue("CurrentUserData", out var ud) == true && ud is UserData userData ? userData : null;

        public UserData SetCurrentUserData(UserData  userData)
        {
            contex?.Items["CurrentUserData"] = userData;
            return userData;
        }

        public IDatabase? GetCurrentUserDb() =>
            contex?.Items.TryGetValue("UserDb", out var db) == true && db is IDatabase userDb ?  userDb : null;

        public IDatabase SetCurrentUserDb(IDatabase userDb) 
        {
            contex?.Items["UserDb"] = userDb;
            return userDb;
        }
    }
}