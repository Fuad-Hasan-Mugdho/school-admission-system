using System.Security.Cryptography;

namespace SchoolAdmission.Api.Domain;

/// <summary>
/// Opaque, URL-safe identifiers. The prefix makes ids self-describing in logs and tests
/// ("c_" = class, "h_" = hold); the body is 12 random base32-ish characters (60 bits of entropy).
/// </summary>
public static class Ids
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    public static string NewClassId() => "c_" + Random(12);
    public static string NewHoldId() => "h_" + Random(12);

    private static string Random(int length)
    {
        Span<char> buffer = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(buffer);
    }
}
