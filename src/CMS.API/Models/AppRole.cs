namespace CMS.API.Models;

/// <summary>
/// AppRole response model. Maps to dbo.AppRole.
/// The primary key constraint is on <see cref="RoleId"/> (string); <see cref="Pkid"/> is an
/// IDENTITY surrogate used for display (主代碼).
/// </summary>
public class AppRole
{
    public int Pkid { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int PermissionLevel { get; set; }
    public string? Description { get; set; }

    /// <summary>Number of users assigned this role (subquery over AppUserRole). Populated in list/view.</summary>
    public int UserCount { get; set; }

    /// <summary>Assigned user ids (n-n via AppUserRole). Populated on GET by id.</summary>
    public List<string> UserIds { get; set; } = new();
}
