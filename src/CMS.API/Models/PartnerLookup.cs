namespace CMS.API.Models;

/// <summary>
/// Slim lookup projection of dbo.Partner, used to populate FK dropdowns in entities that
/// reference Partner (e.g. Course, Certification).
/// </summary>
public class PartnerLookup
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
}
