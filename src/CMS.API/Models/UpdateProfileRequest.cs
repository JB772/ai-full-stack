namespace CMS.API.Models;

/// <summary>
/// 更新「我的個人資料」的請求。只有 <see cref="UserName"/> 會被採用 (必填、去除前後空白)。
/// <see cref="UserId"/> 僅為說明用途 — 端點一律以 JWT 中的身分為準，永不採用請求本文中的 UserId。
/// </summary>
public class UpdateProfileRequest
{
    /// <summary>要更新的使用者名稱 (必填，前後空白會被去除)。</summary>
    public string? UserName { get; set; }

    /// <summary>刻意忽略 — 身分永遠取自 JWT，不從本文讀取。</summary>
    public string? UserId { get; set; }
}
