using Microsoft.AspNetCore.Mvc;

namespace HotelChain.Api.Controllers;

[ApiController]
[Route("api/public")]
public class PublicController : ControllerBase
{
    [HttpGet("ping")]
    public IActionResult Ping()
        => Ok(new { message = "pong" });
}