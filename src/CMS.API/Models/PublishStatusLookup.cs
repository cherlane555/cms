namespace CMS.API.Models;

/// <summary>
/// Slim lookup projection of dbo.PublishStatus, used to populate FK dropdowns in
/// entities that reference PublishStatus (e.g. Course).
/// </summary>
public class PublishStatusLookup
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
