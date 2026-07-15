using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IFeaturedPromoItemRepository
{
    Task<IEnumerable<FeaturedPromoItem>> GetAllAsync();
    Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query);
    Task<FeaturedPromoItem?> GetByPkidAsync(int pkid);

    /// <summary>
    /// The (ScheduleOn, TrainingCenter_pkid, Slot) UNIQUE index means a slot can hold at most one item;
    /// the controller calls this before insert/update to return a friendly 409 instead of a SQL error.
    /// </summary>
    Task<bool> SlotTakenAsync(DateOnly scheduleOn, short trainingCenterPkid, byte slot, int excludePkid);

    /// <summary>Returns the IDENTITY-generated pkid.</summary>
    Task<int> CreateAsync(FeaturedPromoItemRequest request);

    Task<bool> UpdateAsync(FeaturedPromoItemRequest request);
    Task<bool> DeleteAsync(int pkid);

    /// <summary>
    /// Moves a slot up (−1) or down (+1) within the same day + training centre. If the target slot is
    /// occupied the two rows swap (via a temp slot, so the UNIQUE index is never momentarily violated).
    /// </summary>
    Task<MoveResult> MoveAsync(int pkid, bool down);
}
