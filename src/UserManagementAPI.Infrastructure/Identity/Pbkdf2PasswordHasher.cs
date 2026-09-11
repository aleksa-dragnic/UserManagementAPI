using System.Globalization;
using System.Security.Cryptography;

using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Infrastructure.Identity;

/// <summary>
/// Interim hasher until M4 PR14 brings Argon2id. PBKDF2-HMAC-SHA256 from the
/// BCL, a random salt per password, iterations at the OWASP recommendation. It
/// is a real algorithm, not a stub, so nothing plaintext ever reaches the
/// password_hash column even before M4 — but it is the weaker choice and the
/// registration in DependencyInjection is what M4 replaces.
///
/// Stored format: pbkdf2-sha256$iterations$salt$hash, all base64. The algorithm
/// tag is what lets M4's hasher recognise and, if needed, re-hash old values.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Algorithm = "pbkdf2-sha256";

    private const int Iterations = 600_000;

    private const int SaltSize = 16;

    private const int KeySize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return string.Join(
            '$',
            Algorithm,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('$');

        if (parts.Length != 4 ||
            parts[0] != Algorithm ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations))
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