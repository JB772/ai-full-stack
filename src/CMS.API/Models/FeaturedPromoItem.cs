namespace CMS.API.Models;

/// <summary>
/// 首頁上稿項目 (FeaturedPromoItem) 回應模型。每筆代表某訓練中心某日某欄位 (Slot 1-3) 排定的一則促銷。
/// 有兩個外鍵 (TrainingCenter、Promotion2)，故帶兩個導覽物件；兩者皆為 NOT NULL，一律 INNER JOIN。
/// </summary>
public class FeaturedPromoItem
{
    /// <summary>主代碼 (int IDENTITY，由資料庫產生)</summary>
    public int Pkid { get; set; }

    /// <summary>上稿日期</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>訓練中心外鍵 (TrainingCenter_pkid 別名)</summary>
    public short TrainingCenterPkid { get; set; }

    /// <summary>欄位 (1-3)。同一 ScheduleOn + TrainingCenter 下唯一。</summary>
    public byte Slot { get; set; }

    /// <summary>促銷外鍵 (Promotion_pkid 別名)</summary>
    public int PromotionPkid { get; set; }

    /// <summary>標題</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>說明</summary>
    public string Description { get; set; } = string.Empty;

    // Nav objects populated by the multi-map JOIN. Both FKs are NOT NULL, so neither is ever null.
    /// <summary>訓練中心 (JOIN TrainingCenter)</summary>
    public FeaturedPromoNavTrainingCenter? TrainingCenter { get; set; }

    /// <summary>促銷 (JOIN Promotion2，顯示 PromoCode)</summary>
    public FeaturedPromoNavPromotion? Promotion { get; set; }
}

/// <summary>訓練中心導覽物件 (FeaturedPromoItem.TrainingCenter)。</summary>
public class FeaturedPromoNavTrainingCenter
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>促銷導覽物件 (FeaturedPromoItem.Promotion)。列表以 PromoCode 呈現 Promotion_pkid。</summary>
public class FeaturedPromoNavPromotion
{
    public int Pkid { get; set; }
    public string PromoCode { get; set; } = string.Empty;
}
