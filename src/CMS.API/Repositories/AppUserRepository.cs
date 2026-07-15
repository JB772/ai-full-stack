using System.Data;
using System.Text.Json;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Security;
using Dapper;

namespace CMS.API.Repositories;

public class AppUserRepository(IDbConnectionFactory connectionFactory) : IAppUserRepository
{
    // PasswordHash is never selected — it is backend-only and must not reach the client.
    private const string SelectColumns = """
        SELECT u.pkid, u.UserId, u.UserName, u.IsActive, u.PasswordUpdatedTime,
               (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
        FROM AppUser u
        """;

    public async Task<IEnumerable<AppUser>> GetAllAsync()
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<AppUser>($"{SelectColumns} ORDER BY u.UserId ASC");
    }

    public async Task<IEnumerable<AppUser>> QueryAsync(AppUserQuery query)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("(u.UserId LIKE @Keyword OR u.UserName LIKE @Keyword)");
            parameters.Add("Keyword", $"%{query.Keyword.Trim()}%");
        }

        if (query.IsActive.HasValue)
        {
            where.Add("u.IsActive = @IsActive");
            parameters.Add("IsActive", query.IsActive.Value);
        }

        var sql = SelectColumns
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
            + " ORDER BY u.UserId ASC";

        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<AppUser>(sql, parameters);
    }

    public async Task<AppUser?> GetByPkidAsync(int pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AppUser>(
            $"{SelectColumns} WHERE u.pkid = @Pkid", new { Pkid = pkid });
    }

    public async Task<bool> UserIdExistsAsync(string userId, int? excludePkid = null)
    {
        const string sql = """
            SELECT COUNT(1) FROM AppUser
            WHERE UserId = @UserId AND (@ExcludePkid IS NULL OR pkid <> @ExcludePkid)
            """;

        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql, new { UserId = userId, ExcludePkid = excludePkid }) > 0;
    }

    public async Task<int> CreateAsync(AppUserRequest request)
    {
        using var conn = connectionFactory.CreateConnection();

        // New accounts start with the system default password (from SysConfig), SHA-256 hashed.
        // PasswordUpdatedTime stays NULL — the password has been set, not yet *updated* by a reset.
        var passwordHash = PasswordHasher.Hash(await GetDefaultPasswordAsync(conn));

        const string sql = """
            INSERT INTO AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
            VALUES (@UserId, @UserName, @IsActive, @PasswordHash, NULL);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            request.UserId,
            request.UserName,
            request.IsActive,
            PasswordHash = passwordHash
        });
    }

    /// <summary>UserId is the natural key referenced by AppUserRole and PasswordHash is backend-only — neither is updated here.</summary>
    public async Task<bool> UpdateAsync(AppUserRequest request)
    {
        const string sql = """
            UPDATE AppUser
            SET UserName = @UserName,
                IsActive = @IsActive
            WHERE pkid = @Pkid;
            """;

        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteAsync(sql, request) > 0;
    }

    public async Task<bool> DeleteAsync(int pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM AppUser WHERE pkid = @Pkid", new { Pkid = pkid }) > 0;
    }

    public async Task<int> GetRoleCountAsync(int pkid)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM AppUserRole ur
            INNER JOIN AppUser u ON u.UserId = ur.UserId
            WHERE u.pkid = @Pkid
            """;

        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql, new { Pkid = pkid });
    }

    public async Task<bool> ResetPasswordAsync(int pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        var passwordHash = PasswordHasher.Hash(await GetDefaultPasswordAsync(conn));

        const string sql = """
            UPDATE AppUser
            SET PasswordHash = @PasswordHash,
                PasswordUpdatedTime = SYSDATETIME()
            WHERE pkid = @Pkid;
            """;

        return await conn.ExecuteAsync(sql, new { Pkid = pkid, PasswordHash = passwordHash }) > 0;
    }

    /// <summary>Reads SysConfig['appConfig'] (a JSON blob) and returns its `defaultPassword` property.</summary>
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
