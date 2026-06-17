namespace SSO.Application.Abstractions;

/// <summary>
/// Encrypts/decrypts a per-user TOTP secret at rest. Backed by the ASP.NET Core
/// Data Protection API so the raw Base32 secret is never stored in the database.
/// (In production the protection keys themselves are escrowed to an HSM / Vault —
/// see architecture doc §6.3.)
/// </summary>
public interface IMfaSecretProtector
{
    string Protect(string plaintextSecret);
    string Unprotect(string protectedSecret);
}
