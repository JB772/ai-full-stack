namespace CMS.API.Models;

/// <summary>登入請求。UserId + 明文密碼；密碼絕不儲存或回傳。</summary>
public class LoginRequest
{
    /// <summary>帳號</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>密碼 (明文，僅用於驗證，不會回傳)</summary>
    public string Password { get; set; } = string.Empty;
}
