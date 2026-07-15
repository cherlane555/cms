namespace CMS.API.Models;

/// <summary>
/// Login credentials posted to <c>POST /api/Auth/login</c>.
/// <see cref="Password"/> is plaintext over TLS and is never persisted or echoed back.
/// </summary>
public class LoginRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? Password { get; set; }
}
