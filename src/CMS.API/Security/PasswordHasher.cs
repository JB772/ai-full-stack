using System.Security.Cryptography;
using System.Text;

namespace CMS.API.Security;

/// <summary>
/// SHA-256 密碼雜湊。抽成靜態小工具方便單元測試 (雜湊結果可預期)。
/// </summary>
public static class PasswordHasher
{
    /// <summary>回傳 UTF-8 明文密碼的 SHA-256 雜湊，以大寫十六進位字串表示 (64 字元)。</summary>
    public static string Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
