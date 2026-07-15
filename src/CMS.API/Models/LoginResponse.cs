namespace CMS.API.Models;

/// <summary>登入成功後回傳的使用者資訊 (含 JWT)。絕不含 PasswordHash。</summary>
public class LoginResponse
{
    /// <summary>帳號</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>使用者名稱</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>已簽章的 JWT 存取權杖</summary>
    public string AccessToken { get; set; } = string.Empty;
}
