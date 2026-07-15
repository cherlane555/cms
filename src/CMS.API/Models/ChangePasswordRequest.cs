namespace CMS.API.Models;

/// <summary>
/// Body for <c>POST /api/Auth/change-password</c>. All plaintext over TLS; nothing is hashed
/// client-side and no hash ever crosses the wire. The target user is resolved from the JWT.
/// </summary>
public class ChangePasswordRequest
{
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
    public string? ConfirmPassword { get; set; }
}
