using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Security;

/// <summary>
/// Wires the JWT bearer handler's validation parameters to the runtime signing key. Tokens are
/// HS256, minted without issuer/audience, so only the signature and lifetime are validated.
/// </summary>
public class ConfigureJwtBearerOptions : IPostConfigureOptions<JwtBearerOptions>
{
    private readonly IJwtSigningKeyProvider _keyProvider;

    public ConfigureJwtBearerOptions(IJwtSigningKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role,
            // Resolve the key per validation so it always reflects the provider (which caches).
            IssuerSigningKeyResolver = (_, _, _, _) =>
                new[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_keyProvider.GetSigningKey())) },
        };
    }
}
