using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAuthRepository
{
    /// <summary>
    /// 驗證帳密。僅當 UserId 相符、IsActive = 1、且 PasswordHash 與傳入的雜湊完全相同時，
    /// 回傳含角色的使用者；否則回傳 null (呼叫端一律以通用 401 回應，不揭露失敗原因)。
    /// </summary>
    Task<AuthenticatedUser?> AuthenticateAsync(string userId, string passwordHash);

    /// <summary>取得 JWT 簽章金鑰 (SysConfig['appConfig'] JSON 內的 symmetricSecurityKey)。</summary>
    Task<string> GetSigningKeyAsync();

    /// <summary>
    /// 更新指定使用者的 UserName (UserId 為 AppUser 的自然主鍵)。呼叫端須確保 userId 取自 JWT，
    /// userName 已去除前後空白且非空。回傳更新後含角色的使用者；查無此帳號時回傳 null。
    /// </summary>
    Task<AuthenticatedUser?> UpdateUserNameAsync(string userId, string userName);

    /// <summary>
    /// 將指定使用者的 PasswordHash 更新為傳入的雜湊，並將 PasswordUpdatedTime 設為現在時間。
    /// 呼叫端須先驗證目前密碼 (透過 <see cref="AuthenticateAsync"/>) 且新密碼已通過複雜度檢查。
    /// 傳入的是「已雜湊」的新密碼；明文密碼永不進入資料層。回傳是否確實更新了一筆資料。
    /// </summary>
    Task<bool> UpdatePasswordAsync(string userId, string newPasswordHash);

    /// <summary>
    /// 將指定使用者的密碼重設為系統預設值 (SysConfig['appConfig'] JSON 內 defaultPassword 的 SHA256)，
    /// 並將 PasswordUpdatedTime 設為現在時間。預設密碼於執行期由 SysConfig 讀取，不寫死於程式碼；
    /// 明文密碼與雜湊永不離開後端。此為 Admin 專屬的使用者管理動作，userId 為目標帳號 (非登入者本人)。
    /// 回傳是否確實更新了一筆資料 (查無此帳號回傳 false)。
    /// </summary>
    Task<bool> ResetPasswordToDefaultAsync(string userId);
}
