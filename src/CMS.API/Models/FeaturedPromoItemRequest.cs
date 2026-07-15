using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>首頁上稿項目 (FeaturedPromoItem) 新增／修改 DTO。</summary>
public class FeaturedPromoItemRequest
{
    /// <summary>
    /// 主代碼。pkid 是 int IDENTITY，由資料庫產生：新增時忽略此欄位 (送 0)，修改時用來指定資料列 (本身不可修改)。
    /// </summary>
    public int Pkid { get; set; }

    /// <summary>上稿日期</summary>
    [Required]
    public DateOnly ScheduleOn { get; set; }

    /// <summary>訓練中心外鍵 (必填)</summary>
    [Range(1, short.MaxValue)]
    public short TrainingCenterPkid { get; set; }

    /// <summary>欄位 (1-3)</summary>
    [Range(1, 3)]
    public byte Slot { get; set; }

    /// <summary>促銷外鍵 (必填)</summary>
    [Range(1, int.MaxValue)]
    public int PromotionPkid { get; set; }

    /// <summary>標題</summary>
    [Required]
    [StringLength(100)]
    public string Topic { get; set; } = string.Empty;

    /// <summary>說明</summary>
    [Required]
    [StringLength(300)]
    public string Description { get; set; } = string.Empty;
}
