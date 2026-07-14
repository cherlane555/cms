namespace CMS.API.Models;

/// <summary>
/// Write DTO for creating / updating a Partner.
/// <see cref="Pkid"/> is IDENTITY: 0 (ignored) on create, the key on update.
/// </summary>
public class PartnerRequest
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string NameOnPartnerMenu { get; set; } = string.Empty;
    public string NameOnCourseDetailPage { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? ImageFilename { get; set; }
}
