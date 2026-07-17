using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Security;

/// <summary>
/// Password hashing. New hashes use PBKDF2-HMACSHA256 with a random per-user salt, encoded as
/// <c>"PBKDF2$&lt;iterations&gt;$&lt;base64 salt&gt;$&lt;base64 hash&gt;"</c> so the iteration count
/// can be raised later without breaking verification of hashes minted at a lower count.
///
/// Existing <c>AppUser.PasswordHash</c> rows seeded since Lab 03 are unsalted SHA-256 lowercase hex
/// (64 chars) with no salt to recover, so they can't be rehashed without the plaintext password.
/// <see cref="Verify"/> accepts both formats; <see cref="IsLegacyFormat"/> tells the caller (login)
/// to persist a fresh <see cref="Hash"/> once it has the plaintext in hand, migrating the row
/// transparently on the user's next successful login.
/// </summary>
public static class PasswordHasher
{
    private const string Prefix = "PBKDF2";
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <summary>Hash a password with the current (PBKDF2) scheme. Use for every new/changed password.</summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>True if a stored hash predates the PBKDF2 scheme (Lab 03's unsalted SHA-256 hex).</summary>
    public static bool IsLegacyFormat(string? storedHash) =>
        !string.IsNullOrEmpty(storedHash) && !storedHash.StartsWith(Prefix + "$", StringComparison.Ordinal);

    /// <summary>Verify a password against either format, using a constant-time digest comparison.</summary>
    public static bool Verify(string? password, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        return IsLegacyFormat(storedHash)
            ? VerifyLegacy(password, storedHash)
            : VerifyPbkdf2(password, storedHash);
    }

    private static bool VerifyLegacy(string? password, string storedHash)
    {
        var suppliedHash = Sha256Hex(password);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(suppliedHash),
            Encoding.UTF8.GetBytes(storedHash.Trim().ToLowerInvariant()));
    }

    private static bool VerifyPbkdf2(string? password, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password ?? string.Empty), salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Legacy (Lab 03) format — unsalted SHA-256 lowercase hex. Exposed only so tests and
    /// migration-adjacent code can build/recognize a legacy-format hash; never use to hash a new
    /// or changed password — use <see cref="Hash"/> instead.</summary>
    public static string Sha256Hex(string? password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password ?? string.Empty));
        return Convert.ToHexStringLower(bytes);
    }
}
