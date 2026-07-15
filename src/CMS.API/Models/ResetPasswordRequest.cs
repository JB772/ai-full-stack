namespace CMS.API.Models;

/// <summary>
/// 管理者將指定使用者密碼重設為系統預設值的請求。只含目標帳號 <see cref="UserId"/>。
/// 呼叫者身分 (是否為 Admin) 由 JWT 判定，與本文無關；預設密碼於後端由 SysConfig 讀取。
/// 明文密碼與雜湊永不經由此端點傳遞 (請求與回應皆不含任何密碼)。
/// </summary>
public class ResetPasswordRequest
{
    /// <summary>要重設密碼的目標帳號 (AppUser.UserId)。</summary>
    public string? UserId { get; set; }
}
