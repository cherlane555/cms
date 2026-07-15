using CMS.API.Models;

namespace CMS.API.Repositories;

public enum MoveSlotResult
{
    Moved,
    NotFound,
    OutOfRange
}

public interface IFeaturedPromoItemRepository
{
    Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, CancellationToken ct = default);
    Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken ct = default);
    Task<bool> IsSlotTakenAsync(DateTime scheduleOn, short trainingCenterPkid, byte slot, int excludePkid, CancellationToken ct = default);
    Task<FeaturedPromoItem> CreateAsync(FeaturedPromoItemRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int pkid, CancellationToken ct = default);
    Task<MoveSlotResult> MoveSlotAsync(int pkid, int delta, CancellationToken ct = default);
}
