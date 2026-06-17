using System.Security.Cryptography;

namespace Server.Services;

/// <summary>
/// Generates a cryptographically-random temporary password that satisfies the
/// Identity password policy (length + upper/lower/digit/symbol). Ambiguous
/// characters (O/0, I/l/1) are excluded so it can be read out to a user.
/// </summary>
public static class PasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Symbols = "!@#$%^&*-_=+";

    public static string Generate(int length = 16)
    {
        if (length < 8) length = 8;
        var all = Upper + Lower + Digits + Symbols;
        var chars = new char[length];

        // Guarantee one of each required class.
        chars[0] = Pick(Upper);
        chars[1] = Pick(Lower);
        chars[2] = Pick(Digits);
        chars[3] = Pick(Symbols);
        for (var i = 4; i < length; i++)
            chars[i] = Pick(all);

        // Fisher-Yates shuffle so the guaranteed chars aren't always first.
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
}
