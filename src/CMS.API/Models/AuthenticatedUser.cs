namespace CMS.API.Models;

/// <summary>
/// 驗證通過的使用者 (後端內部使用，不直接對外輸出)。承載發 JWT 所需的身分與角色，
/// 但刻意不包含 PasswordHash — 密碼雜湊只在 SQL 中比對，不離開資料層。
/// </summary>
public class AuthenticatedUser
{
    /// <summary>帳號</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>此使用者在 AppUserRole 的所有 RoleId</summary>
    public IReadOnlyList<string> RoleIds { get; set; } = [];
}
