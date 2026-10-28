using Microsoft.AspNetCore.Mvc;

namespace laba0Net.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    [HttpGet("me")]
    public IActionResult GetMe() => Ok(new { user = "student", role = "Admin", source = "Monolith:5100" });

    [HttpPost("login")]
    public IActionResult Login([FromBody] object data) => Ok(new { token = "fake-jwt-token", message = "Успішний вхід" });
}