using HotChocolate.AspNetCore.Authorization;

namespace AspNetAuthentication.GraphQL
{
    public class Query
    {
        public string GetValues()
        {
            return "Hello GraphQL!";
        }

        [Authorize]
        public string GetProfile()
        {
            return "Profile";
        }
    }
}
