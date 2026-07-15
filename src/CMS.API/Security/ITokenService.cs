namespace CMS.API.Security;

/// <summary>A signed JWT access token and the instant it expires (UTC).</summary>
public record TokenResult(string AccessToken, DateTime ExpiresAtUtc);

/// <summary>Builds signed JWT access tokens for authenticated users.</summary>
public interface ITokenService
{
    /// <summary>
    /// Mint a JWT carrying the user's id/name plus one role claim per <paramref name="roles"/>
    /// entry, signed (HS256) with <paramref name="signingKey"/> and expiring 24 hours from now.
    /// </summary>
    TokenResult CreateToken(string userId, string userName, IEnumerable<string> roles, string signingKey);
}
