namespace CMS.API.Models;

/// <summary>
/// Slim lookup projection of dbo.CourseGroup, used to populate FK dropdowns in entities that
/// reference CourseGroup (e.g. Course, PartnerCourseGroup).
/// </summary>
public class CourseGroupLookup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
