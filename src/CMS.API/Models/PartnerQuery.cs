namespace CMS.API.Models;

/// <summary>
/// Search DTO for Partner list filtering.
/// </summary>
public class PartnerQuery
{
    /// <summary>LIKE match across Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage.</summary>
    public string? Keyword { get; set; }
}
