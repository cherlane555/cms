using CMS.API.Models;

namespace CMS.API.Repositories;

public enum MoveSlotResult
{
    Moved,
    NotFound,
    OutOfRange
}

/// <summary>Thrown by <see cref="IFeaturedPromoItemRepository.CreateAsync"/> /
/// <see cref="IFeaturedPromoItemRepository.UpdateAsync"/> when the (ScheduleOn, TrainingCenter,
/// Slot) cell is occupied. Raised from inside the write's own transaction — see the repository
/// implementation for why a pre-check on a separate connection isn't sufficient.</summary>
public class SlotConflictException : Exception
{
    public SlotConflictException(string message) : base(message)
    {
    }
}

public interface IFeaturedPromoItemRepository
{
    Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query, CancellationToken ct = default);
    Task<FeaturedPromoItem?> GetByIdAsync(int pkid, CancellationToken ct = default);
    Task<bool> IsSlotTakenAsync(DateTime scheduleOn, short trainingCenterPkid, byte slot, int excludePkid, CancellationToken ct = default);

    /// <summary>Throws <see cref="SlotConflictException"/> if the slot is occupied.</summary>
    Task<FeaturedPromoItem> CreateAsync(FeaturedPromoItemRequest request, CancellationToken ct = default);

    /// <summary>Throws <see cref="SlotConflictException"/> if the slot is occupied by a different row.</summary>
    Task<bool> UpdateAsync(FeaturedPromoItemRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int pkid, CancellationToken ct = default);
    Task<MoveSlotResult> MoveSlotAsync(int pkid, int delta, CancellationToken ct = default);
}
