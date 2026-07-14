namespace CMS.API.Models;

/// <summary>課程群組 (CourseGroup) 回應模型。</summary>
public class CourseGroup
{
    /// <summary>主代碼 (smallint IDENTITY，由資料庫產生)</summary>
    public short Pkid { get; set; }

    /// <summary>群組說明</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 課程數 (Course 關聯筆數)。
    /// FK_Course_CourseGroup 是 ON DELETE CASCADE：刪除群組會連帶刪掉底下所有課程，
    /// 資料庫本身不會擋。此欄位是刪除前的唯一防線，不只是列表欄位。
    /// </summary>
    public int CourseCount { get; set; }

    /// <summary>夥伴課程群組數 (PartnerCourseGroup 關聯筆數)</summary>
    public int PartnerCourseGroupCount { get; set; }
}
