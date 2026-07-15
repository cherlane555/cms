namespace CMS.API.Security;

/// <summary>
/// Supplies the symmetric key used to validate incoming JWTs — the same
/// <c>symmetricSecurityKey</c> from SysConfig that <see cref="ITokenService"/> signs with.
/// </summary>
public interface IJwtSigningKeyProvider
{
    string GetSigningKey();
}
