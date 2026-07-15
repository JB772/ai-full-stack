using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IPromotionRepository
{
    /// <summary>促銷清單 (依 PromoCode 排序)，供上稿編輯表單以 PromoCode 解析 Promotion_pkid。</summary>
    Task<IEnumerable<PromotionLookup>> GetAllAsync();
}
