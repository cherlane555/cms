using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Security;

/// <summary>Default <see cref="ITokenService"/> — HS256-signed JWTs with a 24-hour lifetime.</summary>
public class TokenService : ITokenService
{
    /// <summary>Token lifetime: 24 hours after issue.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public TokenResult CreateToken(string userId, string userName, IEnumerable<string> roles, string signingKey)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(Lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("userId", userId),
            new("userName", userName),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return new TokenResult(token, expires);
    }
}
