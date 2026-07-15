using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class CourseGroupRepository : ICourseGroupRepository
{
    private readonly IDbConnectionFactory _factory;

    public CourseGroupRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
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

        // pkid is smallint IDENTITY: excluded from the column list, read back via SCOPE_IDENTITY().
        var pkid = await conn.ExecuteScalarAsync<short>(new CommandDefinition(@"
INSERT INTO CourseGroup (Description)
VALUES (@Description);
SELECT CAST(SCOPE_IDENTITY() AS smallint);",
            new { request.Description }, cancellationToken: ct));

        return new CourseGroup
        {
            Pkid = pkid,
            Description = request.Description
        };
    }

    public async Task<bool> UpdateAsync(CourseGroupRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE CourseGroup
SET Description = @Description
WHERE pkid = @Pkid;",
            new { request.Pkid, request.Description }, cancellationToken: ct));

        return affected > 0;
    }

    // NOTE: FK_Course_CourseGroup is ON DELETE CASCADE — deleting a group also deletes every Course
    // filed under it. FK_PartnerCourseGroup_CourseGroup has no cascade, so a group still referenced
    // there fails with SqlException 547. Neither is special-cased here; see spec/course/CourseGroup.md.
    public async Task<bool> DeleteAsync(short pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseGroup WHERE pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
        return affected > 0;
    }
}
