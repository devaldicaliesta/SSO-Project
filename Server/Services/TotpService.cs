using OtpNet;
using QRCoder;

namespace Server.Services;

/// <summary>
/// TOTP (RFC 6238) helper for the dev IdP's second factor. Produces the
/// otpauth:// URI + QR image consumed by Microsoft Authenticator (or any TOTP
/// app), and verifies the 6-digit codes it generates.
/// </summary>
public class TotpService
{
    public const string Issuer = "FundAdmin Dev SSO";

    /// <summary>otpauth:// URI understood by Microsoft Authenticator.</summary>
    public string BuildOtpAuthUri(string secretBase32, string account)
    {
        var label = Uri.EscapeDataString($"{Issuer}:{account}");
        var issuer = Uri.EscapeDataString(Issuer);
        // SHA1 / 6 digits / 30s are Microsoft Authenticator's defaults.
        return $"otpauth://totp/{label}?secret={secretBase32}&issuer={issuer}" +
               "&algorithm=SHA1&digits=6&period=30";
    }

    /// <summary>Renders the otpauth URI as a base64 PNG data URI (no System.Drawing).</summary>
    public string GenerateQrPngDataUri(string otpAuthUri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(8);
        return "data:image/png;base64," + Convert.ToBase64String(bytes);
    }

    /// <summary>Verifies a 6-digit code with a +/- 1 time-step tolerance.</summary>
    public bool VerifyCode(string secretBase32, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        try
        {
            var key = Base32Encoding.ToBytes(secretBase32);
            var totp = new Totp(key);
            return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Groups the secret in 4-char blocks for easy manual entry.</summary>
    public static string FormatManualKey(string secretBase32) =>
        string.Join(" ", Enumerable
            .Range(0, (secretBase32.Length + 3) / 4)
            .Select(i => secretBase32.Substring(i * 4, Math.Min(4, secretBase32.Length - i * 4))));
}
