using Microsoft.AspNetCore.DataProtection;

namespace Server.Services;

/// <summary>
/// Carries the "password verified, MFA pending" state between the Login, Mfa and
/// ChangePassword pages as a tamper-proof, time-limited token (instead of a
/// TempData cookie, which proved unreliable across the redirect). The token
/// encodes the subject id + post-login return URL and expires after a few minutes.
/// </summary>
public sealed class LoginStateProtector
{
    private const string Purpose = "SSO.Login.PendingMfa.v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly ITimeLimitedDataProtector _protector;

    public LoginStateProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    public string Protect(string userId, string returnUrl)
        => _protector.Protect($"{userId}|{returnUrl}", Lifetime);

    public bool TryUnprotect(string? token, out string userId, out string returnUrl)
    {
        userId = string.Empty;
        returnUrl = "~/";

        if (string.IsNullOrEmpty(token))
            return false;

        try
        {
            var payload = _protector.Unprotect(token);
            var separator = payload.IndexOf('|');      // userId is a GUID, never contains '|'
            if (separator <= 0)
                return false;

            userId = payload[..separator];
            returnUrl = payload[(separator + 1)..];
            return true;
        }
        catch
        {
            return false; // tampered or expired
        }
    }
}
