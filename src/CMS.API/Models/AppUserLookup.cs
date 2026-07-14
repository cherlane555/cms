namespace CMS.API.Models;

/// <summary>
/// Slim lookup projection of dbo.AppUser used to populate the role's user multi-select.
/// </summary>
public class AppUserLookup
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    /// <summary>Display label: "UserName (UserId)".</summary>
    public string Label => $"{UserName} ({UserId})";
}
