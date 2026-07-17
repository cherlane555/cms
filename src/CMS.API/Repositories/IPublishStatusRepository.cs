using CMS.API.Models;

namespace CMS.API.Repositories;

/// <summary>Thrown by <see cref="IPublishStatusRepository.CreateAsync"/> when pkid already
/// exists. Raised from a caught PK-violation on the insert itself, closing the race window
/// between the controller's ExistsAsync pre-check (a separate, earlier read) and the write.</summary>
public class PublishStatusConflictException : Exception
{
    public PublishStatusConflictException(string message) : base(message)
    {
    }
}

public interface IPublishStatusRepository
{
    Task<IEnumerable<PublishStatus>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query, CancellationToken ct = default);
    Task<PublishStatus?> GetByIdAsync(byte pkid, CancellationToken ct = default);
    Task<bool> ExistsAsync(byte pkid, CancellationToken ct = default);
    Task<PublishStatus> CreateAsync(PublishStatusRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(PublishStatusRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(byte pkid, CancellationToken ct = default);
}
