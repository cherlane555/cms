namespace CMS.API.Models;

/// <summary>
/// Search DTO for AppRole list filtering.
/// </summary>
public class AppRoleQuery
{
    /// <summary>LIKE match across RoleId, RoleName, Description.</summary>
    public string? Keyword { get; set; }

    /// <summary>Exact match on PermissionLevel (optional).</summary>
    public int? PermissionLevel { get; set; }
}
