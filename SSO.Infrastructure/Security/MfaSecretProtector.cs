using Microsoft.AspNetCore.DataProtection;
using SSO.Application.Abstractions;

namespace SSO.Infrastructure.Security;

/// <summary>
/// Protects per-user TOTP secrets using the Data Protection API. The protector is
/// scoped by a stable purpose string; rotating the underlying keys does not break
/// existing values (the key ring keeps revoked keys for unprotect).
/// </summary>
public sealed class MfaSecretProtector : IMfaSecretProtector
{
    private const string Purpose = "SSO.Mfa.TotpSecret.v1";

    private readonly IDataProtector _protector;

    public MfaSecretProtector(IDataProtectionProvider provider)
        => _protector = provider.CreateProtector(Purpose);

    public string Protect(string plaintextSecret) => _protector.Protect(plaintextSecret);

    public string Unprotect(string protectedSecret) => _protector.Unprotect(protectedSecret);
}
