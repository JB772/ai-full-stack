namespace CMS.API.Models;

/// <summary>角色 (AppRole) 查詢條件。</summary>
public class AppRoleQuery
{
    /// <summary>關鍵字：比對 RoleId、RoleName、Description</summary>
    public string? Keyword { get; set; }

    /// <summary>權限等級下限</summary>
    public int? PermissionLevelFrom { get; set; }

    /// <summary>權限等級上限</summary>
    public int? PermissionLevelTo { get; set; }
}
