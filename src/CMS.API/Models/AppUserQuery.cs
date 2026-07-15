namespace CMS.API.Models;

/// <summary>使用者 (AppUser) 查詢條件。</summary>
public class AppUserQuery
{
    /// <summary>關鍵字：比對 UserId、UserName</summary>
    public string? Keyword { get; set; }

    /// <summary>啟用狀態 (三態：null = 全部，true = 啟用，false = 停用)</summary>
    public bool? IsActive { get; set; }
}
