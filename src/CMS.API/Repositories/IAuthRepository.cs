using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Data access for the login flow: the user (with roles) and the JWT signing key.</summary>
public interface IAuthRepository
{
    /// <summary>
    /// The AppUser matching <paramref name="userId"/> exactly, with its role ids loaded.
    /// Returns <c>null</c> when no such user exists. Active/inactive is reported via
    /// <see cref="AuthUser.IsActive"/> — the caller enforces it — so the credential checks
    /// stay together and every failure can share one generic 401.
    /// </summary>
    Task<AuthUser?> GetLoginUserAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// The <c>symmetricSecurityKey</c> from the <c>appConfig</c> JSON in SysConfig, read at
    /// runtime. Throws <see cref="InvalidOperationException"/> if the row/property is missing.
    /// </summary>
    Task<string> GetSigningKeyAsync(CancellationToken ct = default);
}
