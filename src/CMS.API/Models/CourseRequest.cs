using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>課程 (Course) 新增／修改 DTO。</summary>
public class CourseRequest
{
    /// <summary>
    /// 主代碼。pkid 是 int IDENTITY，由資料庫產生：新增時忽略此欄位 (送 0)，修改時用來指定資料列 (本身不可修改)。
    /// </summary>
    public int Pkid { get; set; }

    /// <summary>課程名稱</summary>
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>官方課程名稱</summary>
    [StringLength(300)]
    public string? OfficialTitle { get; set; }

    /// <summary>簡介代碼</summary>
    [Required]
    [StringLength(50)]
    public string CourseId { get; set; } = string.Empty;

    /// <summary>科目代碼</summary>
    [Required]
    [StringLength(50)]
    public string ProdCourseId { get; set; } = string.Empty;

    /// <summary>友善網址</summary>
    [Required]
    [StringLength(100)]
    public string FriendlyUrl { get; set; } = string.Empty;

    /// <summary>顯示順序</summary>
    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }

    /// <summary>原廠外鍵 (必填)</summary>
    [Range(1, short.MaxValue)]
    public short PartnerPkid { get; set; }

    /// <summary>課程群組外鍵 (可為 null)</summary>
    public short? CourseGroupPkid { get; set; }

    /// <summary>上架狀態外鍵 (必填)</summary>
    public byte PublishStatusPkid { get; set; }

    /// <summary>上架日期</summary>
    [Required]
    public DateOnly ScheduleOn { get; set; }

    /// <summary>下架日期</summary>
    [Required]
    public DateOnly ScheduleOff { get; set; }

    /// <summary>時數</summary>
    [Range(0, short.MaxValue)]
    public short Hour { get; set; }

    /// <summary>定價 (decimal(9,0))</summary>
    [Range(0, 999999999)]
    public decimal ListPrice { get; set; }

    /// <summary>點數 (decimal(9,1))</summary>
    [Range(0, 99999999.9)]
    public decimal LearningCredit { get; set; }

    /// <summary>教材</summary>
    [StringLength(500)]
    public string? Material { get; set; }

    /// <summary>課程目標</summary>
    [StringLength(4000)]
    public string? Objective { get; set; }

    /// <summary>適合對象</summary>
    [StringLength(500)]
    public string? Target { get; set; }

    /// <summary>先備知識</summary>
    [StringLength(4000)]
    public string? Prerequisites { get; set; }

    /// <summary>課程大綱 (nvarchar(max) — 不限長度)</summary>
    public string? Outline { get; set; }

    /// <summary>考試／認證說明 (nvarchar(max) — 不限長度)</summary>
    public string? TowardCertOrExam { get; set; }

    /// <summary>備註</summary>
    [StringLength(4000)]
    public string? Note { get; set; }

    /// <summary>其他資訊</summary>
    [StringLength(4000)]
    public string? OtherInfo { get; set; }

    /// <summary>允許重聽</summary>
    public bool CanRepeat { get; set; }
}
