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

    public async Task<string> GetSigningKeyAsync(CancellationToken ct = default)
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
        if (!doc.RootElement.TryGetProperty("symmetricSecurityKey", out var keyElement)
            || keyElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("'symmetricSecurityKey' is missing from the appConfig JSON.");
        }

        var key = keyElement.GetString();
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("'symmetricSecurityKey' in the appConfig JSON is empty.");
        }

        return key;
    }
}
