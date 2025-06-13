using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MicroLedger.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet("secure-endpoint")]
    [Authorize]
    public IActionResult SecureEndpoint()
    {
        return Ok(new { message = "You have access to the secure endpoint!" });
    }
} 