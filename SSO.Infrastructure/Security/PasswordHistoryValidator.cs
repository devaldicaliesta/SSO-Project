using Microsoft.AspNetCore.Identity;
using SSO.Application.Abstractions;
using SSO.Domain.Identity;

namespace SSO.Infrastructure.Security;

/// <summary>
/// Identity password validator that rejects reuse of a recent password. Registered
/// alongside the built-in validators, so it runs on every create / change / reset.
/// </summary>
public sealed class PasswordHistoryValidator : IPasswordValidator<ApplicationUser>
{
    private readonly IPasswordHistoryService _history;

    public PasswordHistoryValidator(IPasswordHistoryService history) => _history = history;

    public async Task<IdentityResult> ValidateAsync(
        UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (string.IsNullOrEmpty(password))
            return IdentityResult.Success; // length/required handled by other validators

        if (await _history.IsReusedAsync(user, password))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordReuse",
                Description = "You cannot reuse one of your recent passwords."
            });
        }

        return IdentityResult.Success;
    }
}
