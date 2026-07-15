using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ITrainingCenterRepository
{
    /// <summary>訓練中心清單 (依 DisplayOrder、pkid 排序)，供上稿列表的分頁使用。</summary>
    Task<IEnumerable<TrainingCenterLookup>> GetAllAsync();
}
