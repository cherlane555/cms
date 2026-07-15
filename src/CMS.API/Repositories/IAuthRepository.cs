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

    /// <summary>
    /// The <c>defaultPassword</c> from the <c>appConfig</c> JSON in SysConfig, read at runtime.
    /// Throws <see cref="InvalidOperationException"/> if the row/property is missing.
    /// </summary>
    Task<string> GetDefaultPasswordAsync(CancellationToken ct = default);

    /// <summary>
    /// Update only the <c>UserName</c> of the given user. Returns <c>false</c> if no row matched.
    /// Never touches UserId, roles, or the password.
    /// </summary>
    Task<bool> UpdateUserNameAsync(string userId, string userName, CancellationToken ct = default);

    /// <summary>
    /// Set the user's <c>PasswordHash</c> and stamp <c>PasswordUpdatedTime</c> to now.
    /// Returns <c>false</c> if no row matched.
    /// </summary>
    Task<bool> UpdatePasswordAsync(string userId, string passwordHash, CancellationToken ct = default);
}
