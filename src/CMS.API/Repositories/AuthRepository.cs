using System.Data;
using System.Text.Json;
using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Security;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>
/// 登入相關資料存取。密碼雜湊只在 SQL WHERE 子句中比對 —— PasswordHash 從不 SELECT 出來，
/// 不會離開資料層。簽章金鑰於執行期由 SysConfig 讀取，不寫死在程式碼。
///
/// 本類別的三個 AppUser 寫入 (改名 / 改密碼 / 重設密碼) 皆依 CLAUDE.md 的硬性規則稽核：
/// 與異動同一條連線、同一個交易 (見 docs/row-audit.md)。
/// </summary>
public class AuthRepository(IDbConnectionFactory connectionFactory, RowAuditWriter auditWriter) : IAuthRepository
{
    private const string TableName = "AppUser";

    /// <summary>
    /// 稽核用的資料列投影。刻意使用 <see cref="AppUser"/> 回應模型 —— 它「沒有」PasswordHash 屬性，
    /// 因此 <c>LogUpdateAsync</c> 的 ActionDesc (變更欄位「名稱」清單) 不可能包含密碼雜湊。
    /// 改密碼會稽核成 ActionDesc = "PasswordUpdatedTime"：足以辨識是密碼異動，且雜湊永遠不會寫進
    /// dbo.RowAudit (該表可由 GET /api/rowaudit 讀取)。
    /// </summary>
    private const string SelectColumns = """
        SELECT u.pkid, u.UserId, u.UserName, u.IsActive, u.PasswordUpdatedTime,
               (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
        FROM AppUser u
        """;

    private const string RolesSql = "SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId";

    /// <summary>以自然主鍵 UserId 載入稽核用的 AppUser 資料列 (含 pkid，供 PrimaryKeyValues 使用)。</summary>
    private static async Task<AppUser?> LoadByUserIdAsync(IDbConnection conn, string userId, IDbTransaction tx)
        => await conn.QuerySingleOrDefaultAsync<AppUser>(
            $"{SelectColumns} WHERE u.UserId = @UserId", new { UserId = userId }, tx);

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
        conn.Open();
        using var tx = conn.BeginTransaction();

        // 依 UserId (自然主鍵) 更新 UserName；查無此帳號時不寫入、回傳 null。
        var before = await LoadByUserIdAsync(conn, userId, tx);
        if (before is null)
        {
            return null;
        }

        await conn.ExecuteAsync(
            "UPDATE AppUser SET UserName = @UserName WHERE UserId = @UserId",
            new { UserId = userId, UserName = userName }, tx);

        var after = await LoadByUserIdAsync(conn, userId, tx);
        await auditWriter.LogUpdateAsync(TableName, before, after!, conn, tx);

        var roles = await conn.QueryAsync<string>(RolesSql, new { UserId = userId }, tx);
        var user = new AuthenticatedUser
        {
            UserId = after!.UserId,
            UserName = after.UserName,
            RoleIds = roles.ToList()
        };

        tx.Commit();
        return user;
    }

    public async Task<bool> UpdatePasswordAsync(string userId, string newPasswordHash)
    {
        using var conn = connectionFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var before = await LoadByUserIdAsync(conn, userId, tx);
        if (before is null)
        {
            return false;
        }

        // 只寫入雜湊，順帶記錄變更時間。用 SYSDATETIME() 與 ResetPasswordToDefaultAsync 一致。
        const string sql = """
            UPDATE AppUser
            SET PasswordHash = @PasswordHash, PasswordUpdatedTime = SYSDATETIME()
            WHERE UserId = @UserId
            """;

        await conn.ExecuteAsync(sql, new { UserId = userId, PasswordHash = newPasswordHash }, tx);

        var after = await LoadByUserIdAsync(conn, userId, tx);
        await auditWriter.LogUpdateAsync(TableName, before, after!, conn, tx);

        tx.Commit();
        return true;
    }

    public async Task<bool> ResetPasswordToDefaultAsync(string userId)
    {
        using var conn = connectionFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var before = await LoadByUserIdAsync(conn, userId, tx);
        if (before is null)
        {
            return false;
        }

        // 預設密碼於執行期由 SysConfig 讀取 (與 AppUserRepository 建立帳號的路徑一致)，再 SHA256 雜湊。
        var passwordHash = PasswordHasher.Hash(await GetDefaultPasswordAsync(conn, tx));

        // 只寫入雜湊，順帶記錄變更時間。用 SYSDATETIME() 與既有的 change/reset-password 路徑一致。
        const string sql = """
            UPDATE AppUser
            SET PasswordHash = @PasswordHash, PasswordUpdatedTime = SYSDATETIME()
            WHERE UserId = @UserId
            """;

        await conn.ExecuteAsync(sql, new { UserId = userId, PasswordHash = passwordHash }, tx);

        var after = await LoadByUserIdAsync(conn, userId, tx);
        await auditWriter.LogUpdateAsync(TableName, before, after!, conn, tx);

        tx.Commit();
        return true;
    }

    /// <summary>
    /// 讀取 SysConfig['appConfig'] (JSON) 並取出其 `defaultPassword` 屬性。
    /// 呼叫端已開啟交易時必須把 <paramref name="tx"/> 傳進來 —— 在有交易的連線上執行未帶交易的指令會擲例外。
    /// </summary>
    private static async Task<string> GetDefaultPasswordAsync(IDbConnection conn, IDbTransaction? tx = null)
    {
        var json = await conn.ExecuteScalarAsync<string?>(
            "SELECT configValue FROM SysConfig WHERE configKey = @Key", new { Key = "appConfig" }, tx);

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
