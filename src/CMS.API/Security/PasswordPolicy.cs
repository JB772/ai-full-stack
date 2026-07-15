namespace CMS.API.Security;

/// <summary>
/// 新密碼複雜度規則。抽成靜態小工具方便單元測試，並讓後端與前端共用同一套判斷語意：
/// 長度至少 8 碼，且大寫英文／小寫英文／數字／符號四種字元類別中至少涵蓋三種。
/// </summary>
public static class PasswordPolicy
{
    /// <summary>密碼最短長度。</summary>
    public const int MinLength = 8;

    /// <summary>四種字元類別中至少須涵蓋的種類數。</summary>
    public const int RequiredCharClasses = 3;

    /// <summary>複雜度不符時對外顯示的雙語訊息 (前端與後端共用同一文字)。</summary>
    public const string ComplexityMessage =
        "密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號 " +
        "(Password must be at least 8 characters and contain at least 3 of the 4 classes: " +
        "uppercase / lowercase / digit / symbol.)";

    /// <summary>
    /// 判斷密碼是否符合複雜度：長度 &gt;= 8，且大寫、小寫、數字、符號四類中至少涵蓋三類。
    /// 符號定義為「非字母且非數字」的字元。
    /// </summary>
    public static bool IsComplexEnough(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return false;
        }

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSymbol = password.Any(c => !char.IsLetterOrDigit(c));

        var classes = (hasUpper ? 1 : 0) + (hasLower ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol ? 1 : 0);
        return classes >= RequiredCharClasses;
    }
}
