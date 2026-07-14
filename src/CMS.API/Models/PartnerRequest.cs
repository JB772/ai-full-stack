using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>合作夥伴 (Partner) 新增／修改 DTO。</summary>
public class PartnerRequest
{
    /// <summary>
    /// 主代碼。pkid 是 smallint IDENTITY，由資料庫產生：
    /// 新增時忽略此欄位，修改時用來指定資料列 (本身不可修改)。
    /// </summary>
    public short Pkid { get; set; }

    /// <summary>名稱</summary>
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>應用代碼 (資料庫未建唯一索引，故不檢查重複)</summary>
    [Required]
    [StringLength(10)]
    public string AppKey { get; set; } = string.Empty;

    /// <summary>夥伴選單顯示名稱</summary>
    [Required]
    [StringLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    /// <summary>課程明細頁顯示名稱</summary>
    [Required]
    [StringLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    /// <summary>顯示順序</summary>
    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; set; }

    /// <summary>圖片檔名</summary>
    [StringLength(50)]
    public string? ImageFilename { get; set; }
}
