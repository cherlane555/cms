namespace CMS.API.Models;

/// <summary>
/// Successful-login payload. Deliberately omits <c>PasswordHash</c> and every other
/// sensitive column — only the signed token and a minimal profile leave the server.
/// </summary>
public class LoginResponse
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
}
