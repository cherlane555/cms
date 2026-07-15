namespace CMS.API.Models;

/// <summary>
/// CourseGroup response model. Maps to dbo.CourseGroup — a flat lookup table (no foreign keys) in the
/// course sub-system, grouping courses into categories. <see cref="Pkid"/> is a <c>smallint</c>
/// IDENTITY surrogate assigned by the DB.
/// </summary>
public class CourseGroup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
