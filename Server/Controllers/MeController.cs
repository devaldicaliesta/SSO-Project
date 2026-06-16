using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace Server.Controllers;

/// <summary>
/// API endpoint that returns the profile of the currently signed-in user.
/// Protected with [Authorize] using the JWT "Bearer" scheme - it can only be
/// reached with a valid access token issued by the OIDC flow.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class MeController : ControllerBase
{
    /// <summary>
    /// GET /api/me
    /// Mengembalikan nama, email, dan role dari klaim user.
    /// </summary>
    [HttpGet]
    public ActionResult<UserProfileDto> Get()
    {
        var name = User.FindFirst("name")?.Value ?? "Unknown";
        var email = User.FindFirst("email")?.Value ?? "Unknown";
        var role = User.FindFirst("role")?.Value ?? "Unknown";

        return Ok(new UserProfileDto
        {
            Name = name,
            Email = email,
            Role = role
        });
    }
}
