using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class AppRoleRepository : IAppRoleRepository
{
    private readonly IDbConnectionFactory _factory;

    public AppRoleRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    private const string BaseSelect = @"
SELECT r.pkid, r.RoleId, r.RoleName, r.PermissionLevel, r.Description,
       (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.RoleId = r.RoleId) AS UserCount
FROM AppRole r";

    public async Task<IEnumerable<AppRole>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<AppRole>(
            new CommandDefinition($"{BaseSelect} ORDER BY r.pkid ASC", cancellationToken: ct));
    }

    public async Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query, CancellationToken ct = default)
    {
        var where = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("(r.RoleId LIKE @Keyword OR r.RoleName LIKE @Keyword OR r.Description LIKE @Keyword)");
            p.Add("@Keyword", $"%{query.Keyword.Trim()}%");
        }

        if (query.PermissionLevel.HasValue)
        {
            where.Add("r.PermissionLevel = @PermissionLevel");
            p.Add("@PermissionLevel", query.PermissionLevel.Value);
        }

        var whereClause = where.Count > 0 ? $"\nWHERE {string.Join(" AND ", where)}" : string.Empty;
        var sql = $"{BaseSelect}{whereClause}\nORDER BY r.pkid ASC";

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<AppRole>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task<AppRole?> GetByIdAsync(string roleId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);

        var role = await conn.QuerySingleOrDefaultAsync<AppRole>(new CommandDefinition(
            $"{BaseSelect}\nWHERE r.RoleId = @RoleId",
            new { RoleId = roleId }, cancellationToken: ct));

        if (role is null)
        {
            return null;
        }

        var userIds = await conn.QueryAsync<string>(new CommandDefinition(
            "SELECT UserId FROM AppUserRole WHERE RoleId = @RoleId ORDER BY UserId",
            new { RoleId = roleId }, cancellationToken: ct));
        role.UserIds = userIds.ToList();

        return role;
    }

    public async Task<bool> ExistsAsync(string roleId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM AppRole WHERE RoleId = @RoleId",
            new { RoleId = roleId }, cancellationToken: ct));
        return count > 0;
    }

    public async Task<AppRole> CreateAsync(AppRoleRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var pkid = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO AppRole (RoleId, RoleName, PermissionLevel, Description)
VALUES (@RoleId, @RoleName, @PermissionLevel, @Description);
SELECT CAST(SCOPE_IDENTITY() AS int);",
            new
            {
                request.RoleId,
                request.RoleName,
                request.PermissionLevel,
                request.Description
            }, tx, cancellationToken: ct));

        await SyncUsersAsync(conn, tx, request.RoleId, request.UserIds, ct);

        tx.Commit();

        return new AppRole
        {
            Pkid = pkid,
            RoleId = request.RoleId,
            RoleName = request.RoleName,
            PermissionLevel = request.PermissionLevel,
            Description = request.Description,
            UserCount = request.UserIds?.Count ?? 0,
            UserIds = request.UserIds ?? new List<string>()
        };
    }

    public async Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE AppRole
SET RoleName = @RoleName,
    PermissionLevel = @PermissionLevel,
    Description = @Description
WHERE RoleId = @RoleId;",
            new
            {
                request.RoleId,
                request.RoleName,
                request.PermissionLevel,
                request.Description
            }, tx, cancellationToken: ct));

        if (affected == 0)
        {
            tx.Rollback();
            return false;
        }

        await SyncUsersAsync(conn, tx, request.RoleId, request.UserIds, ct);

        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(string roleId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE RoleId = @RoleId",
            new { RoleId = roleId }, tx, cancellationToken: ct));

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppRole WHERE RoleId = @RoleId",
            new { RoleId = roleId }, tx, cancellationToken: ct));

        tx.Commit();
        return affected > 0;
    }

    // n-n sync: delete-then-reinsert on the same connection/transaction.
    private static async Task SyncUsersAsync(
        System.Data.IDbConnection conn,
        System.Data.IDbTransaction tx,
        string roleId,
        List<string>? userIds,
        CancellationToken ct)
    {
        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE RoleId = @RoleId",
            new { RoleId = roleId }, tx, cancellationToken: ct));

        if (userIds is { Count: > 0 })
        {
            var rows = userIds
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct()
                .Select(u => new { UserId = u, RoleId = roleId });

            await conn.ExecuteAsync(new CommandDefinition(
                "INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)",
                rows, tx, cancellationToken: ct));
        }
    }
}
