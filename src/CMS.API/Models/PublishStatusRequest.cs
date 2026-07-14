using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>發布狀態 (PublishStatus) 新增／修改 DTO。</summary>
public class PublishStatusRequest
{
    /// <summary>
    /// 主代碼。PublishStatus.pkid 是 tinyint 且「非」IDENTITY，
    /// 所以新增時必須由使用者指定；修改時用來指定資料列 (本身不可修改)。
    /// </summary>
    [Range(0, 255)]
    public byte Pkid { get; set; }

    /// <summary>狀態說明</summary>
    [Required]
    [StringLength(50)]
    public string Description { get; set; } = string.Empty;

    /// <summary>草稿</summary>
    public bool IsDraft { get; set; }

    /// <summary>已發布</summary>
    public bool IsPublished { get; set; }

    /// <summary>已下架</summary>
    public bool IsDiscontinued { get; set; }
}
