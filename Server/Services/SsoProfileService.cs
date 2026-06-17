using System.Security.Claims;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;
using SSO.Application.Abstractions;
using SSO.Domain.Enums;
using SSO.Domain.Identity;

namespace Server.Services;

/// <summary>
/// Supplies the claims IdentityServer puts into tokens and decides whether a
/// subject is still allowed to have active tokens. This is where role and
/// (resolved) <c>permission</c> claims are injected, and where a disabled or
/// locked-out account is cut off even if it still holds a valid session cookie.
/// </summary>
public sealed class SsoProfileService : IProfileService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPermissionResolver _permissions;

    public SsoProfileService(UserManager<ApplicationUser> users, IPermissionResolver permissions)
    {
        _users = users;
        _permissions = permissions;
    }

    public async Task GetProfileDataAsync(ProfileDataRequestContext context)
    {
        var subjectId = context.Subject.GetSubjectId();
        var user = await _users.FindByIdAsync(subjectId);
        if (user is null)
            return;

        var claims = new List<Claim>
        {
            new("name", user.FullName ?? user.UserName ?? string.Empty),
            new("email", user.Email ?? string.Empty),
        };

        foreach (var role in await _users.GetRolesAsync(user))
            claims.Add(new Claim("role", role));

        if (Guid.TryParse(subjectId, out var userId))
            foreach (var permission in await _permissions.ResolveAsync(userId))
                claims.Add(new Claim("permission", permission));

        context.IssuedClaims.AddRange(claims);
    }

    public async Task IsActiveAsync(IsActiveContext context)
    {
        var subjectId = context.Subject.GetSubjectId();
        var user = await _users.FindByIdAsync(subjectId);

        context.IsActive =
            user is not null &&
            user.Status == UserStatus.Active &&
            !await _users.IsLockedOutAsync(user);
    }
}
