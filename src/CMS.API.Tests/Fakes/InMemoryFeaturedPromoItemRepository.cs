using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Stands in for <see cref="FeaturedPromoItemRepository"/> so the endpoints can be exercised without SQL
/// Server. Mirrors the SQL semantics: pkid is an int IDENTITY the DB generates; the two NOT-NULL FK nav
/// objects (TrainingCenter / Promotion) are rebuilt from small label maps; TrainingCenter exact-match +
/// inclusive ScheduleOn range + Slot + keyword (Topic/Description/PromoCode) filters; the
/// (ScheduleOn, TrainingCenter, Slot) UNIQUE guard; and the slot up/down move with adjacent-row swap.
///
/// The seed lays out one week (2026-03-16 Monday .. 2026-03-22 Sunday) for training centre 1 (台北):
/// Monday has all three slots filled (so a delete frees a slot and a move can swap); Tuesday has slots
/// 1 and 2 (slot 3 empty, so a down-move from slot 2 lands on an empty slot). Training centre 2 (新竹)
/// has a single Monday slot 1, to prove the TrainingCenter tab filter isolates centres.
/// </summary>
public class InMemoryFeaturedPromoItemRepository : IFeaturedPromoItemRepository
{
    // pkid -> Name, matching InMemoryTrainingCenterRepository.
    private static readonly Dictionary<short, string> TrainingCenters = new()
    {
        [1] = "台北", [2] = "新竹", [3] = "台中"
    };

    // Promotion_pkid -> PromoCode, matching InMemoryPromotionRepository.
    private static readonly Dictionary<int, string> PromoCodes = new()
    {
        [10] = "20251204_SkillTrainAI",
        [20] = "251211_GoogleAI",
        [30] = "20251215_n8n"
    };

    private static readonly DateOnly Mon = new(2026, 3, 16);
    private static readonly DateOnly Tue = new(2026, 3, 17);

    private readonly List<FeaturedPromoItem> _items =
    [
        new() { Pkid = 1, ScheduleOn = Mon, TrainingCenterPkid = 1, Slot = 1, PromotionPkid = 10, Topic = "成為能AI協作的程式設計師", Description = "轉職就業養成班" },
        new() { Pkid = 2, ScheduleOn = Mon, TrainingCenterPkid = 1, Slot = 2, PromotionPkid = 20, Topic = "Google AI工具一次掌握", Description = "不需技術基礎" },
        new() { Pkid = 3, ScheduleOn = Mon, TrainingCenterPkid = 1, Slot = 3, PromotionPkid = 30, Topic = "n8n自動化三部曲", Description = "從自動化新手到企業級" },
        new() { Pkid = 4, ScheduleOn = Tue, TrainingCenterPkid = 1, Slot = 1, PromotionPkid = 30, Topic = "n8n自動化三部曲", Description = "從自動化新手到企業級" },
        new() { Pkid = 5, ScheduleOn = Tue, TrainingCenterPkid = 1, Slot = 2, PromotionPkid = 20, Topic = "Google AI工具一次掌握", Description = "不需技術基礎" },
        new() { Pkid = 6, ScheduleOn = Mon, TrainingCenterPkid = 2, Slot = 1, PromotionPkid = 10, Topic = "成為能AI協作的程式設計師", Description = "轉職就業養成班" }
    ];

    private int _nextPkid = 7;

    public Task<IEnumerable<FeaturedPromoItem>> GetAllAsync()
        => Task.FromResult<IEnumerable<FeaturedPromoItem>>(Sorted(_items).ToList());

    public Task<IEnumerable<FeaturedPromoItem>> QueryAsync(FeaturedPromoItemQuery query)
    {
        IEnumerable<FeaturedPromoItem> result = _items;

        if (query.TrainingCenterPkid.HasValue)
        {
            result = result.Where(i => i.TrainingCenterPkid == query.TrainingCenterPkid.Value);
        }

        if (query.ScheduleOnFrom.HasValue)
        {
            result = result.Where(i => i.ScheduleOn >= query.ScheduleOnFrom.Value);
        }

        if (query.ScheduleOnTo.HasValue)
        {
            result = result.Where(i => i.ScheduleOn <= query.ScheduleOnTo.Value);
        }

        if (query.Slot.HasValue)
        {
            result = result.Where(i => i.Slot == query.Slot.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            result = result.Where(i =>
                i.Topic.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || i.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || PromoCodes.GetValueOrDefault(i.PromotionPkid, string.Empty)
                    .Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IEnumerable<FeaturedPromoItem>>(Sorted(result).ToList());
    }

    public Task<FeaturedPromoItem?> GetByPkidAsync(int pkid)
    {
        var item = _items.SingleOrDefault(i => i.Pkid == pkid);
        return Task.FromResult(item is null ? null : Project(item));
    }

    public Task<bool> SlotTakenAsync(DateOnly scheduleOn, short trainingCenterPkid, byte slot, int excludePkid)
        => Task.FromResult(_items.Any(i =>
            i.ScheduleOn == scheduleOn
            && i.TrainingCenterPkid == trainingCenterPkid
            && i.Slot == slot
            && i.Pkid != excludePkid));

    public Task<int> CreateAsync(FeaturedPromoItemRequest request)
    {
        var pkid = _nextPkid++;
        _items.Add(new FeaturedPromoItem
        {
            Pkid = pkid,
            ScheduleOn = request.ScheduleOn,
            TrainingCenterPkid = request.TrainingCenterPkid,
            Slot = request.Slot,
            PromotionPkid = request.PromotionPkid,
            Topic = request.Topic,
            Description = request.Description
        });
        return Task.FromResult(pkid);
    }

    public Task<bool> UpdateAsync(FeaturedPromoItemRequest request)
    {
        var item = _items.SingleOrDefault(i => i.Pkid == request.Pkid);
        if (item is null)
        {
            return Task.FromResult(false);
        }

        item.ScheduleOn = request.ScheduleOn;
        item.TrainingCenterPkid = request.TrainingCenterPkid;
        item.Slot = request.Slot;
        item.PromotionPkid = request.PromotionPkid;
        item.Topic = request.Topic;
        item.Description = request.Description;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int pkid)
    {
        var item = _items.SingleOrDefault(i => i.Pkid == pkid);
        return Task.FromResult(item is not null && _items.Remove(item));
    }

    public Task<MoveResult> MoveAsync(int pkid, bool down)
    {
        var item = _items.SingleOrDefault(i => i.Pkid == pkid);
        if (item is null)
        {
            return Task.FromResult(MoveResult.NotFound);
        }

        var target = down ? item.Slot + 1 : item.Slot - 1;
        if (target < 1 || target > 3)
        {
            return Task.FromResult(MoveResult.OutOfRange);
        }

        var sibling = _items.SingleOrDefault(i =>
            i.ScheduleOn == item.ScheduleOn
            && i.TrainingCenterPkid == item.TrainingCenterPkid
            && i.Slot == (byte)target);

        if (sibling is not null)
        {
            sibling.Slot = item.Slot; // swap
        }

        item.Slot = (byte)target;
        return Task.FromResult(MoveResult.Moved);
    }

    private static IEnumerable<FeaturedPromoItem> Sorted(IEnumerable<FeaturedPromoItem> items)
        => items
            .OrderBy(i => i.ScheduleOn)
            .ThenBy(i => i.TrainingCenterPkid)
            .ThenBy(i => i.Slot)
            .Select(Project);

    /// <summary>Clones the row and rebuilds the two nav objects the real multi-map query would JOIN in.</summary>
    private static FeaturedPromoItem Project(FeaturedPromoItem i) => new()
    {
        Pkid = i.Pkid,
        ScheduleOn = i.ScheduleOn,
        TrainingCenterPkid = i.TrainingCenterPkid,
        Slot = i.Slot,
        PromotionPkid = i.PromotionPkid,
        Topic = i.Topic,
        Description = i.Description,
        TrainingCenter = new FeaturedPromoNavTrainingCenter
        {
            Pkid = i.TrainingCenterPkid,
            Name = TrainingCenters.GetValueOrDefault(i.TrainingCenterPkid, $"中心{i.TrainingCenterPkid}")
        },
        Promotion = new FeaturedPromoNavPromotion
        {
            Pkid = i.PromotionPkid,
            PromoCode = PromoCodes.GetValueOrDefault(i.PromotionPkid, $"PROMO{i.PromotionPkid}")
        }
    };
}
