namespace CMS.API.Models;

/// <summary>
/// Partner response model. Maps to dbo.Partner — a flat parent table (no foreign keys) in the
/// course sub-system. <see cref="Pkid"/> is a <c>smallint</c> IDENTITY surrogate assigned by the DB.
/// </summary>
public class Partner
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string NameOnPartnerMenu { get; set; } = string.Empty;
    public string NameOnCourseDetailPage { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? ImageFilename { get; set; }
}
