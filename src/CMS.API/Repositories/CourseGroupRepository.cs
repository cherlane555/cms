using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class CourseGroupRepository : ICourseGroupRepository
{
    private const string TableName = "CourseGroup";

    private readonly IDbConnectionFactory _factory;
    private readonly IRowAuditWriter _audit;

    public CourseGroupRepository(IDbConnectionFactory factory, IRowAuditWriter audit)
    {
        _factory = factory;
        _audit = audit;
    }

    private const string BaseSelect = @"
SELECT g.pkid, g.Description
FROM CourseGroup g";

    public async Task<IEnumerable<CourseGroup>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<CourseGroup>(
            new CommandDefinition($"{BaseSelect} ORDER BY g.Description ASC", cancellationToken: ct));
    }

    public async Task<IEnumerable<CourseGroup>> QueryAsync(CourseGroupQuery query, CancellationToken ct = default)
    {
        var where = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("(g.Description LIKE @Keyword)");
            p.Add("@Keyword", $"%{query.Keyword.Trim()}%");
        }

        var whereClause = where.Count > 0 ? $"\nWHERE {string.Join(" AND ", where)}" : string.Empty;
        var sql = $"{BaseSelect}{whereClause}\nORDER BY g.Description ASC";

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<CourseGroup>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task<CourseGroup?> GetByIdAsync(short pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<CourseGroup>(new CommandDefinition(
            $"{BaseSelect}\nWHERE g.pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
    }

    public async Task<CourseGroup> CreateAsync(CourseGroupRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // pkid is smallint IDENTITY: excluded from the column list, read back via SCOPE_IDENTITY().
        var pkid = await conn.ExecuteScalarAsync<short>(new CommandDefinition(@"
INSERT INTO CourseGroup (Description)
VALUES (@Description);
SELECT CAST(SCOPE_IDENTITY() AS smallint);",
            new { request.Description }, tx, cancellationToken: ct));

        var created = new CourseGroup
        {
            Pkid = pkid,
            Description = request.Description
        };

        await _audit.LogInsertAsync(TableName, created, conn, tx, ct);
        tx.Commit();

        return created;
    }

    public async Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // Load the "before" row first so the audit can list exactly the changed columns.
        var before = await conn.QuerySingleOrDefaultAsync<CourseGroup>(new CommandDefinition(
            $"{BaseSelect}\nWHERE g.pkid = @Pkid",
            new { request.Pkid }, tx, cancellationToken: ct));
        if (before is null)
        {
            return false;
        }

        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE CourseGroup
SET Description = @Description
WHERE pkid = @Pkid;",
            new { request.Pkid, request.Description }, tx, cancellationToken: ct));

        if (affected == 0)
        {
            return false;
        }

        var after = new CourseGroup { Pkid = before.Pkid, Description = request.Description };
        await _audit.LogUpdateAsync(TableName, before, after, conn, tx, ct);
        tx.Commit();
        return true;
    }

    public async Task<int> CountCoursesAsync(short pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM Course WHERE CourseGroup_pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
    }

    // NOTE: FK_Course_CourseGroup is ON DELETE CASCADE — deleting a group also deletes every Course
    // filed under it (the controller guards via CountCoursesAsync). FK_PartnerCourseGroup_CourseGroup
    // has no cascade, so a group still referenced there fails with SqlException 547.
    public async Task<bool> DeleteAsync(short pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // Load the row first so its first string column is still available for the audit.
        var row = await conn.QuerySingleOrDefaultAsync<CourseGroup>(new CommandDefinition(
            $"{BaseSelect}\nWHERE g.pkid = @Pkid",
            new { Pkid = pkid }, tx, cancellationToken: ct));
        if (row is null)
        {
            return false;
        }

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseGroup WHERE pkid = @Pkid",
            new { Pkid = pkid }, tx, cancellationToken: ct));
        if (affected == 0)
        {
            return false;
        }

        await _audit.LogDeleteAsync(TableName, row, conn, tx, ct);
        tx.Commit();
        return true;
    }
}
