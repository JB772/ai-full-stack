namespace CMS.API.Models;

/// <summary>發布狀態 (PublishStatus) 回應模型。</summary>
public class PublishStatus
{
    /// <summary>主代碼 (tinyint，非自動編號，由使用者指定)</summary>
    public byte Pkid { get; set; }

    /// <summary>狀態說明</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>草稿</summary>
    public bool IsDraft { get; set; }

    /// <summary>已發布</summary>
    public bool IsPublished { get; set; }

    /// <summary>已下架</summary>
    public bool IsDiscontinued { get; set; }

    /// <summary>課程數 (Course 關聯筆數)</summary>
    public int CourseCount { get; set; }

    /// <summary>促銷活動數 (Promotion2 關聯筆數)</summary>
    public int PromotionCount { get; set; }
}
