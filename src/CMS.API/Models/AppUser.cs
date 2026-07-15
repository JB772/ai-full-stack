namespace CMS.API.Models;

/// <summary>使用者 (AppUser) 回應模型。PasswordHash 為後端專用，不對外輸出。</summary>
public class AppUser
{
    /// <summary>主代碼</summary>
    public int Pkid { get; set; }

    /// <summary>帳號 (新增後不可修改)</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>啟用</summary>
    public bool IsActive { get; set; }

    /// <summary>密碼更新時間 (尚未重設過為 null)</summary>
    public DateTime? PasswordUpdatedTime { get; set; }

    /// <summary>角色數 (AppUserRole 關聯筆數)</summary>
    public int RoleCount { get; set; }
}
