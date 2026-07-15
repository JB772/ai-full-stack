using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 使用者 (AppUser) 新增／修改 DTO。
/// PasswordHash 由後端處理 (新增時取自 SysConfig 的預設密碼，修改時不變動)，故不在此 DTO 中。
/// </summary>
public class AppUserRequest
{
    /// <summary>主代碼 (新增時忽略，修改時必填)</summary>
    public int Pkid { get; set; }

    /// <summary>帳號 (新增後不可修改)</summary>
    [Required]
    [StringLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱</summary>
    [Required]
    [StringLength(200)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用 (預設為 true)</summary>
    public bool IsActive { get; set; } = true;
}
