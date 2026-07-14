namespace CMS.API.Models;

/// <summary>
/// Write DTO for creating / updating an AppRole (plus its n-n user assignments).
/// </summary>
public class AppRoleRequest
{
    public int Pkid { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int PermissionLevel { get; set; }
    public string? Description { get; set; }

    /// <summary>Assigned user ids (n-n via AppUserRole). Synced delete-then-reinsert.</summary>
    public List<string> UserIds { get; set; } = new();
}
