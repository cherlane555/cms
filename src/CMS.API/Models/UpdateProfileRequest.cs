namespace CMS.API.Models;

/// <summary>
/// Body for <c>PUT /api/Auth/profile</c>. Deliberately carries <b>only</b> the display name —
/// the target user is resolved from the JWT, and roles are never editable here.
/// </summary>
public class UpdateProfileRequest
{
    public string? UserName { get; set; }
}
