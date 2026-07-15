using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// In-memory <see cref="PromotionRepository"/>: three promotions keyed by the same pkids the
/// FeaturedPromoItem fake references (10 / 20 / 30), returned sorted by PromoCode.
/// </summary>
public class InMemoryPromotionRepository : IPromotionRepository
{
    private readonly List<PromotionLookup> _promotions =
    [
        new() { Pkid = 10, PromoCode = "20251204_SkillTrainAI", Topic = "成為能AI協作的程式設計師", Description = "轉職就業養成班" },
        new() { Pkid = 20, PromoCode = "251211_GoogleAI", Topic = "Google AI工具一次掌握", Description = "不需技術基礎" },
        new() { Pkid = 30, PromoCode = "20251215_n8n", Topic = "n8n自動化三部曲", Description = "從自動化新手到企業級" }
    ];

    public Task<IEnumerable<PromotionLookup>> GetAllAsync()
        => Task.FromResult<IEnumerable<PromotionLookup>>(
            _promotions.OrderBy(p => p.PromoCode, StringComparer.Ordinal).ToList());
}
