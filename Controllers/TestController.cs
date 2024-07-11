using Microsoft.AspNetCore.Mvc;

namespace NotesHubApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok("Test successful");
        }
    }
}
