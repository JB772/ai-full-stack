using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>課程群組 (CourseGroup) 新增／修改 DTO。</summary>
public class CourseGroupRequest
{
    /// <summary>
    /// 主代碼。pkid 是 smallint IDENTITY，由資料庫產生：
    /// 新增時忽略此欄位，修改時用來指定資料列 (本身不可修改)。
    /// </summary>
    public short Pkid { get; set; }

    /// <summary>群組說明 (資料庫未建唯一索引，故不檢查重複)</summary>
    [Required]
    [StringLength(100)]
    public string Description { get; set; } = string.Empty;
}
