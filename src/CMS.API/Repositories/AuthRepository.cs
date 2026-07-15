using System.Text.Json;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _factory;

    public AuthRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<AuthUser?> GetLoginUserAsync(string userId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);

        using var multi = await conn.QueryMultipleAsync(new CommandDefinition(@"
SELECT UserId, UserName, PasswordHash, IsActive FROM AppUser WHERE UserId = @UserId;
SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId;",
            new { UserId = userId }, cancellationToken: ct));

        var user = await multi.ReadSingleOrDefaultAsync<AuthUser>();
        if (user is null)
        {
            return null;
        }

        var roles = await multi.ReadAsync<string>();
        user.Roles = roles.ToList();
        return user;
    }

    public Task<string> GetSigningKeyAsync(CancellationToken ct = default) =>
        GetAppConfigStringAsync("symmetricSecurityKey", ct);

    public Task<string> GetDefaultPasswordAsync(CancellationToken ct = default) =>
        GetAppConfigStringAsync("defaultPassword", ct);

    // Read a string property from the appConfig JSON in SysConfig at runtime.
    private async Task<string> GetAppConfigStringAsync(string propertyName, CancellationToken ct)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);

        var json = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT configValue FROM SysConfig WHERE configKey = 'appConfig'",
            cancellationToken: ct));

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("SysConfig 'appConfig' row was not found.");
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty(propertyName, out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"'{propertyName}' is missing from the appConfig JSON.");
        }

        var value = element.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"'{propertyName}' in the appConfig JSON is empty.");
        }

        return value;
    }

    public async Task<bool> UpdateUserNameAsync(string userId, string userName, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE AppUser SET UserName = @UserName WHERE UserId = @UserId",
            new { UserId = userId, UserName = userName }, cancellationToken: ct));

        return affected > 0;
    }

    public async Task<bool> UpdatePasswordAsync(string userId, string passwordHash, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE AppUser SET PasswordHash = @PasswordHash, PasswordUpdatedTime = GETUTCDATE() WHERE UserId = @UserId",
            new { UserId = userId, PasswordHash = passwordHash }, cancellationToken: ct));

        return affected > 0;
    }
}
