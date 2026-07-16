namespace CMS.API.Models;

/// <summary>使用者的單一角色 (含角色名稱) — AppUser 角色清單 API 的回應項目。</summary>
public class UserRole
{
    /// <summary>角色代碼 (AppRole.RoleId)。</summary>
    public string RoleId { get; set; } = string.Empty;

    /// <summary>角色名稱 (AppRole.RoleName)。</summary>
    public string RoleName { get; set; } = string.Empty;
}

/// <summary>指派角色給使用者的請求 body。</summary>
public class AssignRoleRequest
{
    /// <summary>要指派的角色代碼 (AppRole.RoleId)。</summary>
    public string RoleId { get; set; } = string.Empty;
}
