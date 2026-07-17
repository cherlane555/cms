using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace CMS.API.Repositories;

public class AppRoleRepository : IAppRoleRepository
{
    private const string TableName = "AppRole";

    private readonly IDbConnectionFactory _factory;
    private readonly IRowAuditWriter _audit;

    public AppRoleRepository(IDbConnectionFactory factory, IRowAuditWriter audit)
    {
        _factory = factory;
        _audit = audit;
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

        // The controller's ExistsAsync check runs on a separate, earlier connection — a
        // concurrent create for the same RoleId can race past it. RoleId's own PRIMARY KEY
        // constraint is the real backstop; catch its violation here instead of letting it
        // surface as a raw 500.
        int pkid;
        try
        {
            pkid = await conn.ExecuteScalarAsync<int>(new CommandDefinition(@"
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
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            throw new RoleConflictException($"Role '{request.RoleId}' already exists.");
        }

        await SyncUsersAsync(conn, tx, request.RoleId, request.UserIds, ct);

        var created = new AppRole
        {
            Pkid = pkid,
            RoleId = request.RoleId,
            RoleName = request.RoleName,
            PermissionLevel = request.PermissionLevel,
            Description = request.Description,
            UserCount = request.UserIds?.Count ?? 0,
            UserIds = request.UserIds ?? new List<string>()
        };

        await _audit.LogInsertAsync(TableName, created, conn, tx, ct);
        tx.Commit();

        return created;
    }

    public async Task<bool> UpdateAsync(AppRoleRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // Load the "before" row first so the audit can list exactly the changed columns.
        var before = await conn.QuerySingleOrDefaultAsync<AppRole>(new CommandDefinition(
            $"{BaseSelect}\nWHERE r.RoleId = @RoleId",
            new { request.RoleId }, tx, cancellationToken: ct));
        if (before is null)
        {
            return false;
        }

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

        // The "after" image: the before row with the updatable columns applied. UserCount /
        // UserIds are copied from before so association changes don't show up as columns.
        var after = new AppRole
        {
            Pkid = before.Pkid,
            RoleId = before.RoleId,
            RoleName = request.RoleName,
            PermissionLevel = request.PermissionLevel,
            Description = request.Description,
            UserCount = before.UserCount,
            UserIds = before.UserIds
        };

        await _audit.LogUpdateAsync(TableName, before, after, conn, tx, ct);
        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(string roleId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // Load the row first so its first string column is still available for the audit.
        var row = await conn.QuerySingleOrDefaultAsync<AppRole>(new CommandDefinition(
            $"{BaseSelect}\nWHERE r.RoleId = @RoleId",
            new { RoleId = roleId }, tx, cancellationToken: ct));
        if (row is null)
        {
            return false;
        }

        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppUserRole WHERE RoleId = @RoleId",
            new { RoleId = roleId }, tx, cancellationToken: ct));

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM AppRole WHERE RoleId = @RoleId",
            new { RoleId = roleId }, tx, cancellationToken: ct));
        if (affected == 0)
        {
            return false;
        }

        await _audit.LogDeleteAsync(TableName, row, conn, tx, ct);
        tx.Commit();
        return true;
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
