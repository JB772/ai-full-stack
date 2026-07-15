namespace CMS.API.Models;

/// <summary>課程 (Course) 回應模型。Course 是本專案第一個有外鍵的資料表，故也是第一個帶導覽物件的模型。</summary>
public class Course
{
    /// <summary>主代碼 (int IDENTITY，由資料庫產生)</summary>
    public int Pkid { get; set; }

    /// <summary>課程名稱</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>官方課程名稱</summary>
    public string? OfficialTitle { get; set; }

    /// <summary>簡介代碼</summary>
    public string CourseId { get; set; } = string.Empty;

    /// <summary>科目代碼</summary>
    public string ProdCourseId { get; set; } = string.Empty;

    /// <summary>友善網址</summary>
    public string FriendlyUrl { get; set; } = string.Empty;

    /// <summary>顯示順序</summary>
    public int DisplayOrder { get; set; }

    /// <summary>原廠外鍵 (Partner_pkid 別名)</summary>
    public short PartnerPkid { get; set; }

    /// <summary>課程群組外鍵 (CourseGroup_pkid 別名，可為 null)</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>上架狀態外鍵 (PublishStatus_pkid 別名)</summary>
    public byte PublishStatusPkid { get; set; }

    /// <summary>上架日期</summary>
    public DateOnly ScheduleOn { get; set; }

    /// <summary>下架日期</summary>
    public DateOnly ScheduleOff { get; set; }

    /// <summary>時數</summary>
    public short Hour { get; set; }

    /// <summary>定價 (decimal(9,0))</summary>
    public decimal ListPrice { get; set; }

    /// <summary>點數 (decimal(9,1))</summary>
    public decimal LearningCredit { get; set; }

    /// <summary>教材</summary>
    public string? Material { get; set; }

    /// <summary>課程目標</summary>
    public string? Objective { get; set; }

    /// <summary>適合對象</summary>
    public string? Target { get; set; }

    /// <summary>先備知識</summary>
    public string? Prerequisites { get; set; }

    /// <summary>課程大綱 (nvarchar(max))</summary>
    public string? Outline { get; set; }

    /// <summary>考試／認證說明 (nvarchar(max))</summary>
    public string? TowardCertOrExam { get; set; }

    /// <summary>備註</summary>
    public string? Note { get; set; }

    /// <summary>其他資訊</summary>
    public string? OtherInfo { get; set; }

    /// <summary>允許重聽</summary>
    public bool CanRepeat { get; set; }

    // Nav objects populated by the multi-map JOIN. CourseGroup is null when the FK is null.
    /// <summary>原廠 (JOIN Partner)</summary>
    public CourseNavPartner? Partner { get; set; }

    /// <summary>課程群組 (LEFT JOIN CourseGroup，可為 null)</summary>
    public CourseNavCourseGroup? CourseGroup { get; set; }

    /// <summary>上架狀態 (JOIN PublishStatus)</summary>
    public CourseNavPublishStatus? PublishStatus { get; set; }

    // Delete-guard counts. Any non-zero value makes the API refuse the delete with a 409.
    // CertificationCount / JobCategoryCount matter most: those two FKs are ON DELETE CASCADE, so
    // without the guard the DELETE would silently destroy the junction rows.
    /// <summary>課程問答數 (CourseFAQ)</summary>
    public int CourseFaqCount { get; set; }

    /// <summary>認證關聯數 (CourseInCertification，ON DELETE CASCADE)</summary>
    public int CertificationCount { get; set; }

    /// <summary>職類關聯數 (CourseJobCategories，ON DELETE CASCADE)</summary>
    public int JobCategoryCount { get; set; }

    /// <summary>相關連結數 (CourseRelatedLink)</summary>
    public int RelatedLinkCount { get; set; }

    /// <summary>熱門課程數 (HotCourse)</summary>
    public int HotCourseCount { get; set; }

    /// <summary>推薦課程數 (CourseRecomm，以 CourseId 關聯)</summary>
    public int RecommCount { get; set; }
}

/// <summary>原廠導覽物件 (Course.Partner)。只帶顯示所需的欄位。</summary>
public class CourseNavPartner
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>課程群組導覽物件 (Course.CourseGroup)。</summary>
public class CourseNavCourseGroup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>上架狀態導覽物件 (Course.PublishStatus)。</summary>
public class CourseNavPublishStatus
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
