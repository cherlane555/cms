using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class PartnerRepository : IPartnerRepository
{
    private readonly IDbConnectionFactory _factory;

    public PartnerRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
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
            }, cancellationToken: ct));

        return new Partner
        {
            Pkid = pkid,
            Name = request.Name,
            AppKey = request.AppKey,
            NameOnPartnerMenu = request.NameOnPartnerMenu,
            NameOnCourseDetailPage = request.NameOnCourseDetailPage,
            DisplayOrder = request.DisplayOrder,
            ImageFilename = request.ImageFilename
        };
    }

    public async Task<bool> UpdateAsync(PartnerRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
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
            }, cancellationToken: ct));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(short pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM Partner WHERE pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
        return affected > 0;
    }
}
