using System.Globalization;
using System.Security.Cryptography;

namespace Commerce.Application.Auth;

/// <summary>PBKDF2 with SHA-256. Format: PBKDF2-SHA256$iterations$salt$hash (salt and hash in Base64).</summary>
public static class PasswordHasher
{
    public const int DefaultIterations = 600_000;

    private const string Prefix = "PBKDF2-SHA256";
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password, int iterations = DefaultIterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashSize);

        return string.Join('$', Prefix, iterations.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4
            || parts[0] != Prefix
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
            || iterations < 1)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
