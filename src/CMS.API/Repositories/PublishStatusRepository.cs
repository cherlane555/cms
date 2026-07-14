using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class PublishStatusRepository : IPublishStatusRepository
{
    private readonly IDbConnectionFactory _factory;

    public PublishStatusRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    private const string BaseSelect = @"
SELECT s.pkid, s.Description, s.IsDraft, s.IsPublished, s.IsDiscontinued
FROM PublishStatus s";

    public async Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<PublishStatus>(
            new CommandDefinition($"{BaseSelect} ORDER BY s.pkid ASC", cancellationToken: ct));
    }

    public async Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken ct = default)
    {
        var where = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("s.Description LIKE @Keyword");
            p.Add("@Keyword", $"%{query.Keyword.Trim()}%");
        }

        if (query.IsDraft.HasValue)
        {
            where.Add("s.IsDraft = @IsDraft");
            p.Add("@IsDraft", query.IsDraft.Value);
        }

        if (query.IsPublished.HasValue)
        {
            where.Add("s.IsPublished = @IsPublished");
            p.Add("@IsPublished", query.IsPublished.Value);
        }

        if (query.IsDiscontinued.HasValue)
        {
            where.Add("s.IsDiscontinued = @IsDiscontinued");
            p.Add("@IsDiscontinued", query.IsDiscontinued.Value);
        }

        var whereClause = where.Count > 0 ? $"\nWHERE {string.Join(" AND ", where)}" : string.Empty;
        var sql = $"{BaseSelect}{whereClause}\nORDER BY s.pkid ASC";

        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<PublishStatus>(new CommandDefinition(sql, p, cancellationToken: ct));
    }

    public async Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PublishStatus>(new CommandDefinition(
            $"{BaseSelect}\nWHERE s.pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
    }

    public async Task<bool> ExistsAsync(byte pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var count = await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM PublishStatus WHERE pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
        return count > 0;
    }

    public async Task<PublishStatus> CreateAsync(PublishStatusRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);

        // pkid is a user-assigned tinyint (NOT IDENTITY): it is written explicitly and there is
        // no SCOPE_IDENTITY() to read back.
        await conn.ExecuteAsync(new CommandDefinition(@"
INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);",
            new
            {
                request.Pkid,
                request.Description,
                request.IsDraft,
                request.IsPublished,
                request.IsDiscontinued
            }, cancellationToken: ct));

        return new PublishStatus
        {
            Pkid = request.Pkid,
            Description = request.Description,
            IsDraft = request.IsDraft,
            IsPublished = request.IsPublished,
            IsDiscontinued = request.IsDiscontinued
        };
    }

    public async Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(@"
UPDATE PublishStatus
SET Description = @Description,
    IsDraft = @IsDraft,
    IsPublished = @IsPublished,
    IsDiscontinued = @IsDiscontinued
WHERE pkid = @Pkid;",
            new
            {
                request.Pkid,
                request.Description,
                request.IsDraft,
                request.IsPublished,
                request.IsDiscontinued
            }, cancellationToken: ct));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(byte pkid, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM PublishStatus WHERE pkid = @Pkid",
            new { Pkid = pkid }, cancellationToken: ct));
        return affected > 0;
    }
}
