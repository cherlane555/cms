using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ILookupRepository
{
    Task<IEnumerable<AppUserLookup>> GetAppUsersAsync(CancellationToken ct = default);
    Task<IEnumerable<PublishStatusLookup>> GetPublishStatusesAsync(CancellationToken ct = default);
    Task<IEnumerable<PartnerLookup>> GetPartnersAsync(CancellationToken ct = default);
}
