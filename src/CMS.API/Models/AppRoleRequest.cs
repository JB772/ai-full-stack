using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>角色 (AppRole) 新增／修改 DTO。</summary>
public class AppRoleRequest
{
    /// <summary>主代碼 (新增時忽略，修改時必填)</summary>
    public int Pkid { get; set; }

    /// <summary>角色代碼 (新增後不可修改)</summary>
    [Required]
    [StringLength(200)]
    public string RoleId { get; set; } = string.Empty;

    /// <summary>角色名稱</summary>
    [Required]
    [StringLength(200)]
    public string RoleName { get; set; } = string.Empty;

    /// <summary>權限等級</summary>
    [Range(0, int.MaxValue)]
    public int PermissionLevel { get; set; } = 100;

    /// <summary>描述</summary>
    [StringLength(400)]
    public string? Description { get; set; }
}
