namespace CMS.API.Models;

/// <summary>
/// Internal auth projection of dbo.AppUser (plus the user's role ids). Used only inside the
/// login flow to verify credentials and build token claims — it is never returned to a client,
/// so it may carry <see cref="PasswordHash"/>.
/// </summary>
public class AuthUser
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    /// <summary>RoleId values from AppUserRole for this user; become role claims on the JWT.</summary>
    public List<string> Roles { get; set; } = new();
}
