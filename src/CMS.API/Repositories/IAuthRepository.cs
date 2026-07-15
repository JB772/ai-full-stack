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
}
