using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Konscious.Security.Cryptography;

using Microsoft.Extensions.Options;

using UserManagementAPI.Application.Abstractions;

namespace UserManagementAPI.Infrastructure.Identity;

/// <summary>
/// Argon2id with a random salt per password. Stored in a PHC-style string that
/// carries the algorithm, version and cost parameters, so Verify reads the
/// parameters from the hash rather than from configuration — raising the
/// configured cost affects new hashes only and never invalidates an old one.
///
/// Format: $argon2id$v=19$m=&lt;kb&gt;,t=&lt;iterations&gt;,p=&lt;lanes&gt;$&lt;salt&gt;$&lt;hash&gt;
/// (salt and hash base64).
/// </summary>
public sealed class Argon2PasswordHasher(IOptions<Argon2Options> options) : IPasswordHasher
{
    private const string Algorithm = "argon2id";

    private const int Version = 19;

    private const int SaltSize = 16;

    private const int KeySize = 32;

    private readonly Argon2Options _options = options.Value;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        var key = Derive(
            password,
            salt,
            _options.MemorySizeKb,
            _options.Iterations,
            _options.DegreeOfParallelism,
            KeySize);

        return string.Create(CultureInfo.InvariantCulture,
            $"${Algorithm}$v={Version}$m={_options.MemorySizeKb},t={_options.Iterations},p={_options.DegreeOfParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}");
    }

    public bool Verify(string password, string hash)
    {
        // Leading '$' produces an empty first segment.
        var parts = hash.Split('$');

        if (parts.Length != 6 || parts[0].Length != 0 || parts[1] != Algorithm || parts[2] != $"v={Version}")
        {
            return false;
        }

        if (!TryParseParameters(parts[3], out var memoryKb, out var iterations, out var parallelism))
        {
            return false;
        }

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[4]);
            expected = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, memoryKb, iterations, parallelism, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static bool TryParseParameters(string segment, out int memoryKb, out int iterations, out int parallelism)
    {
        memoryKb = iterations = parallelism = 0;

        var pairs = segment.Split(',');

        if (pairs.Length != 3)
        {
            return false;
        }

        return TryParsePair(pairs[0], "m", out memoryKb)
            && TryParsePair(pairs[1], "t", out iterations)
            && TryParsePair(pairs[2], "p", out parallelism)
            && memoryKb > 0 && iterations > 0 && parallelism > 0;
    }

    private static bool TryParsePair(string pair, string key, out int value)
    {
        value = 0;

        var separator = pair.IndexOf('=');

        return separator > 0
            && pair[..separator] == key
            && int.TryParse(pair[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static byte[] Derive(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int length)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKb,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };

        return argon2.GetBytes(length);
    }
}