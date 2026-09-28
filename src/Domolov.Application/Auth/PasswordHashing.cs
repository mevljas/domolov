using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Domolov.Application.Auth;

/// <summary>PBKDF2-SHA256 password hashes in the form <c>pbkdf2-sha256$iterations$salt$hash</c>.</summary>
public static class PasswordHashing
{
    private const string Prefix = "pbkdf2-sha256";
    private const int DefaultIterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static string Hash(string password, int iterations = DefaultIterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashBytes
        );
        return string.Join(
            '$',
            Prefix,
            iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash)
        );
    }

    public static bool Verify(string password, string encoded)
    {
        var parts = encoded.Trim().Split('$');
        if (
            parts.Length != 4
            || parts[0] != Prefix
            || !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var iterations
            )
            || iterations < 1
        )
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length
            );
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Constant-time comparison of a plain password.</summary>
    public static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)),
            SHA256.HashData(Encoding.UTF8.GetBytes(b))
        );
}
