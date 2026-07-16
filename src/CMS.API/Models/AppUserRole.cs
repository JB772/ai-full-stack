namespace CMS.API.Models;

/// <summary>
/// AppUserRole 關聯列 (使用者 ⇄ 角色 的 N-N 中介)。僅供 RowAudit 稽核使用：
/// 反射取 pkid 作 PrimaryKeyValues、首個字串屬性 (UserId) 作 ActionDesc。
/// </summary>
public class AppUserRole
{
    public int Pkid { get; set; }

    /// <summary>被指派角色的使用者帳號 (AppUser.UserId 自然鍵)。</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>角色代碼 (AppRole.RoleId 自然鍵)。</summary>
    public string RoleId { get; set; } = string.Empty;
}
