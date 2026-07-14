namespace CMS.API.Models;

/// <summary>合作夥伴 (Partner) 回應模型。</summary>
public class Partner
{
    /// <summary>主代碼 (smallint IDENTITY，由資料庫產生)</summary>
    public short Pkid { get; set; }

    /// <summary>名稱</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>應用代碼</summary>
    public string AppKey { get; set; } = string.Empty;

    /// <summary>夥伴選單顯示名稱</summary>
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    /// <summary>課程明細頁顯示名稱</summary>
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    /// <summary>顯示順序</summary>
    public int DisplayOrder { get; set; }

    /// <summary>圖片檔名</summary>
    public string? ImageFilename { get; set; }

    /// <summary>課程數 (Course 關聯筆數)</summary>
    public int CourseCount { get; set; }

    /// <summary>認證數 (Certification 關聯筆數)</summary>
    public int CertificationCount { get; set; }

    /// <summary>課程群組數 (PartnerCourseGroup 關聯筆數)</summary>
    public int CourseGroupCount { get; set; }

    /// <summary>促銷活動數 (Promotion2.RelatedPartner_pkid 關聯筆數)</summary>
    public int PromotionCount { get; set; }

    /// <summary>研討會數 (Seminar 關聯筆數，資料庫未建外鍵)</summary>
    public int SeminarCount { get; set; }
}
