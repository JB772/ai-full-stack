namespace CMS.API.Models;

/// <summary>首頁上稿項目 (FeaturedPromoItem) 查詢條件。列表主要以「訓練中心分頁 + 一週日期範圍」篩選。</summary>
public class FeaturedPromoItemQuery
{
    /// <summary>訓練中心外鍵 (精確比對，對應 UI 上的分頁)</summary>
    public short? TrainingCenterPkid { get; set; }

    /// <summary>上稿日期下限 (含，通常為該週週一)</summary>
    public DateOnly? ScheduleOnFrom { get; set; }

    /// <summary>上稿日期上限 (含，通常為該週週日)</summary>
    public DateOnly? ScheduleOnTo { get; set; }

    /// <summary>欄位 (精確比對，1-3)</summary>
    public byte? Slot { get; set; }

    /// <summary>關鍵字：比對 Topic、Description、Promotion2.PromoCode</summary>
    public string? Keyword { get; set; }
}
