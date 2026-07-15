namespace CMS.API.Models;

/// <summary>
/// Write DTO for creating / updating a CourseGroup.
/// <see cref="Pkid"/> is IDENTITY: 0 (ignored) on create, the key on update.
/// </summary>
public class CourseGroupRequest
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
