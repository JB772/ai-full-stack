namespace CMS.API.Models;

/// <summary>「我的個人資料」回應。UserId 與 Roles 皆為唯讀顯示，僅 UserName 可更新。</summary>
public class ProfileResponse
{
    /// <summary>帳號 (唯讀)。</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>此使用者的所有角色 (唯讀顯示)。</summary>
    public IReadOnlyList<string> Roles { get; set; } = [];
}
