namespace CMS.API.Models;

/// <summary>合作夥伴 (Partner) 查詢條件。</summary>
public class PartnerQuery
{
    /// <summary>關鍵字：比對 Name、AppKey、NameOnPartnerMenu、NameOnCourseDetailPage</summary>
    public string? Keyword { get; set; }

    /// <summary>顯示順序下限 (含)</summary>
    public int? DisplayOrderFrom { get; set; }

    /// <summary>顯示順序上限 (含)</summary>
    public int? DisplayOrderTo { get; set; }

    /// <summary>是否有圖片 (null = 不篩選)</summary>
    public bool? HasImage { get; set; }
}
