using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class LookupRepository : ILookupRepository
{
    private readonly IDbConnectionFactory _factory;

    public LookupRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<AppUserLookup>(new CommandDefinition(
            "SELECT UserId, UserName FROM AppUser ORDER BY UserName ASC",
            cancellationToken: ct));
    }

    public async Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<PublishStatusLookup>(new CommandDefinition(
            "SELECT pkid, Description FROM PublishStatus ORDER BY pkid ASC",
            cancellationToken: ct));
    }

    public async Task<IEnumerable<PartnerLookup>> GetPartnersAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<PartnerLookup>(new CommandDefinition(
            "SELECT pkid, Name FROM Partner ORDER BY DisplayOrder ASC",
            cancellationToken: ct));
    }

    public async Task<IEnumerable<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<CourseGroupLookup>(new CommandDefinition(
            "SELECT pkid, Description FROM CourseGroup ORDER BY Description ASC",
            cancellationToken: ct));
    }
}
