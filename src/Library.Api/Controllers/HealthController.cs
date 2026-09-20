using Microsoft.AspNetCore.Mvc;

namespace Library.Api.Controllers
{
    [ApiController]
    [Route("health")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class HealthController : ControllerBase
    {
        [HttpGet("")]
        public IActionResult Get()
        {
            return Ok(new HealthResponse("healthy"));
        }
    }

    /// <summary>A type, not anonymous: JSON declared, not inferred.</summary>
    public sealed class HealthResponse
    {
        public HealthResponse(string status)
        {
            Status = status;
        }

        public string Status { get; }
    }
}
