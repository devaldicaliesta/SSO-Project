using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace Server.Controllers;

/// <summary>
/// API endpoint that returns the profile of the currently signed-in user.
/// In the BFF model this is a LOCAL api endpoint: the browser calls it with the
/// HttpOnly session cookie (no bearer token in the browser). It is protected by
/// [Authorize] (default "cookie" scheme) and the BFF antiforgery requirement
/// (applied via .AsBffApiEndpoint() in Program.cs).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
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
