namespace CMS.API.Models;

/// <summary>The signed-in user's profile after a successful update (never includes secrets).</summary>
public class ProfileResponse
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
}
