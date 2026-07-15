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

    public async Task<IEnumerable<TrainingCenterLookup>> GetTrainingCentersAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<TrainingCenterLookup>(new CommandDefinition(
            "SELECT pkid, Name FROM TrainingCenter ORDER BY DisplayOrder ASC",
            cancellationToken: ct));
    }

    public async Task<IEnumerable<CertificationLookup>> GetCertificationsAsync(CancellationToken ct = default)
    {
        // Title is nchar(100) NULL -> RTRIM + coalesce.
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<CertificationLookup>(new CommandDefinition(
            "SELECT pkid, ISNULL(RTRIM(Title), '') AS Title FROM Certification ORDER BY RTRIM(Title) ASC",
            cancellationToken: ct));
    }

    public async Task<IEnumerable<JobCategoryLookup>> GetJobCategoriesAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QueryAsync<JobCategoryLookup>(new CommandDefinition(
            "SELECT pkid, Description FROM JobCategory ORDER BY Description ASC",
            cancellationToken: ct));
    }

    public async Task<PromotionLookup?> GetPromotionByCodeAsync(string promoCode, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateOpenConnectionAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PromotionLookup>(new CommandDefinition(
            "SELECT pkid, PromoCode, Topic, Description FROM Promotion2 WHERE PromoCode = @PromoCode",
            new { PromoCode = promoCode }, cancellationToken: ct));
    }
}
