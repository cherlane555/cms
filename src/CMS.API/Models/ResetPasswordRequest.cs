namespace CMS.API.Models;

/// <summary>
/// Body for <c>POST /api/Auth/reset-password</c> (Admin only). Carries only the target user's
/// id — the default password is read server-side from SysConfig; no password/hash crosses the wire.
/// </summary>
public class ResetPasswordRequest
{
    public string? UserId { get; set; }
}
