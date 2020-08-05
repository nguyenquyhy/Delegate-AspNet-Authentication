using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetAuthentication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        public string Get()
        {
            return "Hello API!";
        }

        [Authorize]
        [Route("Profile")]
        public string Profile()
        {
            return "Profile";
        }
    }
}
