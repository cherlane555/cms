using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class PartnerRepository : IPartnerRepository
{
    private const string TableName = "Partner";

    private readonly IDbConnectionFactory _factory;
    private readonly IRowAuditWriter _audit;

    public PartnerRepository(IDbConnectionFactory factory, IRowAuditWriter audit)
    {
        _factory = factory;
        _audit = audit;
    }

    private const string BaseSelect = @"
SELECT p.pkid, p.Name, p.AppKey, p.NameOnPartnerMenu, p.NameOnCourseDetailPage,
       p.DisplayOrder, p.ImageFilename
FROM Partner p";

    public async Task<IEnumerable<Partner>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<Partner>(
            new CommandDefinition($"{BaseSelect} ORDER BY p.DisplayOrder ASC", cancellationToken: ct));
    }

    public async Task<IEnumerable<Partner>> QueryAsync(PartnerQuery query, CancellationToken ct = default)
    {
        var where = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add(@"(p.Name LIKE @Keyword OR p.AppKey LIKE @Keyword
                        OR p.NameOnPartnerMenu LIKE @Keyword OR p.NameOnCourseDetailPage LIKE @Keyword)");
            p.Add("@Keyword", $"%{query.Keyword.Trim()}%");
        }

        var whereClause = where.Count > 0 ? $"\nWHERE {string.Join(" AND ", where)}" : string.Empty;
        var sql = $"{BaseSelect}{whereClause}\nORDER BY p.DisplayOrder ASC";

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<Partner>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task<Partner?> GetByIdAsync(short pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<Partner>(new CommandDefinition(
            $"{BaseSelect}\nWHERE p.pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
    }

    public async Task<Partner> CreateAsync(PartnerRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // pkid is smallint IDENTITY: excluded from the column list, read back via SCOPE_IDENTITY().
        var pkid = await conn.ExecuteScalarAsync<short>(new CommandDefinition(@"
INSERT INTO Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
SELECT CAST(SCOPE_IDENTITY() AS smallint);",
            new
            {
                request.Name,
                request.AppKey,
                request.NameOnPartnerMenu,
                request.NameOnCourseDetailPage,
                request.DisplayOrder,
                request.ImageFilename
            }, tx, cancellationToken: ct));

        var created = new Partner
        {
            Pkid = pkid,
            Name = request.Name,
            AppKey = request.AppKey,
            NameOnPartnerMenu = request.NameOnPartnerMenu,
            NameOnCourseDetailPage = request.NameOnCourseDetailPage,
            DisplayOrder = request.DisplayOrder,
            ImageFilename = request.ImageFilename
        };

        await _audit.LogInsertAsync(TableName, created, conn, tx, ct);
        tx.Commit();

        return created;
    }

    public async Task<bool> UpdateAsync(PartnerRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // Load the "before" row first so the audit can list exactly the changed columns.
        var before = await conn.QuerySingleOrDefaultAsync<Partner>(new CommandDefinition(
            $"{BaseSelect}\nWHERE p.pkid = @Pkid",
            new { request.Pkid }, tx, cancellationToken: ct));
        if (before is null)
        {
            return false;
        }

        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE Partner
SET Name = @Name,
    AppKey = @AppKey,
    NameOnPartnerMenu = @NameOnPartnerMenu,
    NameOnCourseDetailPage = @NameOnCourseDetailPage,
    DisplayOrder = @DisplayOrder,
    ImageFilename = @ImageFilename
WHERE pkid = @Pkid;",
            new
            {
                request.Pkid,
                request.Name,
                request.AppKey,
                request.NameOnPartnerMenu,
                request.NameOnCourseDetailPage,
                request.DisplayOrder,
                request.ImageFilename
            }, tx, cancellationToken: ct));

        if (affected == 0)
        {
            return false;
        }

        await _audit.LogUpdateAsync(TableName, before, ApplyRequest(before, request), conn, tx, ct);
        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(short pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        using var tx = conn.BeginTransaction();

        // Load the row first so its first string column is still available for the audit.
        var row = await conn.QuerySingleOrDefaultAsync<Partner>(new CommandDefinition(
            $"{BaseSelect}\nWHERE p.pkid = @Pkid",
            new { Pkid = pkid }, tx, cancellationToken: ct));
        if (row is null)
        {
            return false;
        }

        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Partner WHERE pkid = @Pkid",
            new { Pkid = pkid }, tx, cancellationToken: ct));
        if (affected == 0)
        {
            return false;
        }

        await _audit.LogDeleteAsync(TableName, row, conn, tx, ct);
        tx.Commit();
        return true;
    }

    /// <summary>The "after" image for the audit diff: the before row with the updatable columns applied.</summary>
    private static Partner ApplyRequest(Partner before, PartnerRequest request) => new()
    {
        Pkid = before.Pkid,
        Name = request.Name,
        AppKey = request.AppKey,
        NameOnPartnerMenu = request.NameOnPartnerMenu,
        NameOnCourseDetailPage = request.NameOnCourseDetailPage,
        DisplayOrder = request.DisplayOrder,
        ImageFilename = request.ImageFilename
    };
}
