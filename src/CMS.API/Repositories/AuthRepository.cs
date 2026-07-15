using System.Data;
using System.Text.Json;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Security;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>
/// 登入相關資料存取。密碼雜湊只在 SQL WHERE 子句中比對 —— PasswordHash 從不 SELECT 出來，
/// 不會離開資料層。簽章金鑰於執行期由 SysConfig 讀取，不寫死在程式碼。
/// </summary>
public class AuthRepository(IDbConnectionFactory connectionFactory) : IAuthRepository
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(string userId, string passwordHash)
    {
        using var conn = connectionFactory.CreateConnection();

        // 三個條件全部在 SQL 內比對：UserId 相符、啟用中、且密碼雜湊一致。
        // 只選出可對外的欄位，PasswordHash 永不外流。
        const string userSql = """
            SELECT UserId, UserName
            FROM AppUser
            WHERE UserId = @UserId AND IsActive = 1 AND PasswordHash = @PasswordHash
            """;

        var user = await conn.QuerySingleOrDefaultAsync<AuthenticatedUser>(
            userSql, new { UserId = userId, PasswordHash = passwordHash });

        if (user is null)
        {
            return null;
        }

        const string rolesSql = "SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId";
        var roles = await conn.QueryAsync<string>(rolesSql, new { user.UserId });
        user.RoleIds = roles.ToList();

        return user;
    }

    /// <summary>讀取 SysConfig['appConfig'] (JSON) 並取出其 `symmetricSecurityKey` 屬性。</summary>
    public async Task<string> GetSigningKeyAsync()
    {
        using var conn = connectionFactory.CreateConnection();

        var json = await conn.ExecuteScalarAsync<string?>(
            "SELECT configValue FROM SysConfig WHERE configKey = @Key", new { Key = "appConfig" });

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("找不到系統設定 appConfig，無法取得 JWT 簽章金鑰。");
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("symmetricSecurityKey", out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(value.GetString()))
        {
            throw new InvalidOperationException("系統設定 appConfig 缺少 symmetricSecurityKey。");
        }

        return value.GetString()!;
    }

    public async Task<AuthenticatedUser?> UpdateUserNameAsync(string userId, string userName)
    {
        using var conn = connectionFactory.CreateConnection();

        // 依 UserId (自然主鍵) 更新 UserName，再讀回可對外的欄位；查無此帳號時 SELECT 回傳 null。
        const string sql = """
            UPDATE AppUser SET UserName = @UserName WHERE UserId = @UserId;
            SELECT UserId, UserName FROM AppUser WHERE UserId = @UserId;
            """;

        var user = await conn.QuerySingleOrDefaultAsync<AuthenticatedUser>(
            sql, new { UserId = userId, UserName = userName });

        if (user is null)
        {
            return null;
        }

        const string rolesSql = "SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId";
        var roles = await conn.QueryAsync<string>(rolesSql, new { user.UserId });
        user.RoleIds = roles.ToList();

        return user;
    }

    public async Task<bool> UpdatePasswordAsync(string userId, string newPasswordHash)
    {
        using var conn = connectionFactory.CreateConnection();

        // 只寫入雜湊，順帶記錄變更時間。用 SYSDATETIME() 與 ResetPasswordToDefaultAsync 一致。
        const string sql = """
            UPDATE AppUser
            SET PasswordHash = @PasswordHash, PasswordUpdatedTime = SYSDATETIME()
            WHERE UserId = @UserId
            """;

        var affected = await conn.ExecuteAsync(sql, new { UserId = userId, PasswordHash = newPasswordHash });
        return affected > 0;
    }

    public async Task<bool> ResetPasswordToDefaultAsync(string userId)
    {
        using var conn = connectionFactory.CreateConnection();

        // 預設密碼於執行期由 SysConfig 讀取 (與 AppUserRepository 建立帳號的路徑一致)，再 SHA256 雜湊。
        var passwordHash = PasswordHasher.Hash(await GetDefaultPasswordAsync(conn));

        // 只寫入雜湊，順帶記錄變更時間。用 SYSDATETIME() 與既有的 change/reset-password 路徑一致。
        const string sql = """
            UPDATE AppUser
            SET PasswordHash = @PasswordHash, PasswordUpdatedTime = SYSDATETIME()
            WHERE UserId = @UserId
            """;

        var affected = await conn.ExecuteAsync(sql, new { UserId = userId, PasswordHash = passwordHash });
        return affected > 0;
    }

    /// <summary>讀取 SysConfig['appConfig'] (JSON) 並取出其 `defaultPassword` 屬性。</summary>
    private static async Task<string> GetDefaultPasswordAsync(IDbConnection conn)
    {
        var json = await conn.ExecuteScalarAsync<string?>(
            "SELECT configValue FROM SysConfig WHERE configKey = @Key", new { Key = "appConfig" });

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("找不到系統設定 appConfig，無法取得預設密碼。");
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("defaultPassword", out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrEmpty(value.GetString()))
        {
            throw new InvalidOperationException("系統設定 appConfig 缺少 defaultPassword。");
        }

        return value.GetString()!;
    }
}
