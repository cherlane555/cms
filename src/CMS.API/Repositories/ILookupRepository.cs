using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ILookupRepository
{
    Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken ct = default);
    Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken ct = default);
    Task<IEnumerable<PartnerLookup>> GetPartnersAsync(CancellationToken ct = default);
    Task<IEnumerable<CourseGroupLookup>> GetCourseGroupsAsync(CancellationToken ct = default);
    Task<IEnumerable<TrainingCenterLookup>> GetTrainingCentersAsync(CancellationToken ct = default);
    Task<IEnumerable<CertificationLookup>> GetCertificationsAsync(CancellationToken ct = default);
    Task<IEnumerable<JobCategoryLookup>> GetJobCategoriesAsync(CancellationToken ct = default);
    Task<PromotionLookup?> GetPromotionByCodeAsync(string promoCode, CancellationToken ct = default);
}
