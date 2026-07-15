namespace CMS.API.Models;

/// <summary>
/// 變更登入者本人密碼的請求。身分 (UserId) 一律取自 JWT，永不從本文讀取，故此處只含三個密碼欄位。
/// 明文密碼僅用於驗證與雜湊，絕不儲存或回傳；密碼雜湊永不離開後端。
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>目前密碼 (明文)。須與資料庫中的 PasswordHash 相符，否則不予變更。</summary>
    public string? CurrentPassword { get; set; }

    /// <summary>新密碼 (明文)。須符合複雜度規則 (見 <see cref="Security.PasswordPolicy"/>)。</summary>
    public string? NewPassword { get; set; }

    /// <summary>確認新密碼 (明文)。須與 <see cref="NewPassword"/> 完全相同。</summary>
    public string? ConfirmNewPassword { get; set; }
}
