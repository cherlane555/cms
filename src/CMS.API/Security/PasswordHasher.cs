using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Security;

/// <summary>
/// SHA-256 password hashing. Produces lowercase hex to match the format already stored in
/// <c>AppUser.PasswordHash</c> (established by the Lab 03 AppUser create logic).
/// </summary>
public static class PasswordHasher
{
    public static string Sha256Hex(string? password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password ?? string.Empty));
        return Convert.ToHexStringLower(bytes);
    }
}
