using System.Data;
using System.Text.Json;
using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Security;
using Dapper;

namespace CMS.API.Repositories;

public class AppUserRepository(IDbConnectionFactory connectionFactory, RowAuditWriter auditWriter) : IAppUserRepository
{
    private const string TableName = "AppUser";

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
        conn.Open();

        // New accounts start with the system default password (from SysConfig), SHA-256 hashed.
        // PasswordUpdatedTime stays NULL — the password has been set, not yet *updated* by a reset.
        // Read the config before opening the transaction so every later command honours that transaction.
        var passwordHash = PasswordHasher.Hash(await GetDefaultPasswordAsync(conn));

        const string sql = """
            INSERT INTO AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
            VALUES (@UserId, @UserName, @IsActive, @PasswordHash, NULL);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        using var tx = conn.BeginTransaction();

        var pkid = await conn.ExecuteScalarAsync<int>(sql, new
        {
            request.UserId,
            request.UserName,
            request.IsActive,
            PasswordHash = passwordHash
        }, tx);

        var created = await LoadByPkidAsync(conn, pkid, tx);
        await auditWriter.LogInsertAsync(TableName, created!, conn, tx);

        tx.Commit();
        return pkid;
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
        conn.Open();
        using var tx = conn.BeginTransaction();

        var before = await LoadByPkidAsync(conn, request.Pkid, tx);
        if (before is null)
        {
            return false;
        }

        await conn.ExecuteAsync(sql, request, tx);
        var after = await LoadByPkidAsync(conn, request.Pkid, tx);
        await auditWriter.LogUpdateAsync(TableName, before, after!, conn, tx);

        tx.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(int pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var row = await LoadByPkidAsync(conn, pkid, tx);
        if (row is null)
        {
            return false;
        }

        await conn.ExecuteAsync("DELETE FROM AppUser WHERE pkid = @Pkid", new { Pkid = pkid }, tx);
        await auditWriter.LogDeleteAsync(TableName, row, conn, tx);

        tx.Commit();
        return true;
    }

    /// <summary>Loads a row on an existing open connection/transaction (see PublishStatusRepository).
    /// PasswordHash is never selected — the audit only needs pkid and the first string column (UserId).</summary>
    private static async Task<AppUser?> LoadByPkidAsync(IDbConnection conn, int pkid, IDbTransaction tx)
        => await conn.QuerySingleOrDefaultAsync<AppUser>(
            $"{SelectColumns} WHERE u.pkid = @Pkid", new { Pkid = pkid }, tx);

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

    public async Task<IEnumerable<UserRole>> GetRolesAsync(int pkid)
    {
        const string sql = """
            SELECT r.RoleId, r.RoleName
            FROM AppUserRole ur
            INNER JOIN AppUser u ON u.UserId = ur.UserId
            INNER JOIN AppRole r ON r.RoleId = ur.RoleId
            WHERE u.pkid = @Pkid
            ORDER BY r.PermissionLevel ASC, r.RoleId ASC
            """;

        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<UserRole>(sql, new { Pkid = pkid });
    }

    public async Task<bool> RoleExistsAsync(string roleId)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM AppRole WHERE RoleId = @RoleId", new { RoleId = roleId }) > 0;
    }

    public async Task<bool> HasRoleAsync(int pkid, string roleId)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM AppUserRole ur
            INNER JOIN AppUser u ON u.UserId = ur.UserId
            WHERE u.pkid = @Pkid AND ur.RoleId = @RoleId
            """;

        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql, new { Pkid = pkid, RoleId = roleId }) > 0;
    }

    public async Task AssignRoleAsync(int pkid, string roleId)
    {
        // Resolve UserId from pkid and insert on the same transaction as the audit row, so a failed
        // insert leaves no audit. SCOPE_IDENTITY gives the new AppUserRole pkid for the audit entry.
        const string sql = """
            INSERT INTO AppUserRole (UserId, RoleId)
            SELECT u.UserId, @RoleId FROM AppUser u WHERE u.pkid = @Pkid;
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        using var conn = connectionFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var rolePkid = await conn.ExecuteScalarAsync<int>(sql, new { Pkid = pkid, RoleId = roleId }, tx);
        var row = await LoadRoleRowAsync(conn, rolePkid, tx);
        await auditWriter.LogInsertAsync("AppUserRole", row!, conn, tx);

        tx.Commit();
    }

    public async Task<bool> RemoveRoleAsync(int pkid, string roleId)
    {
        using var conn = connectionFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var row = await conn.QuerySingleOrDefaultAsync<AppUserRole>(
            """
            SELECT ur.pkid, ur.UserId, ur.RoleId
            FROM AppUserRole ur
            INNER JOIN AppUser u ON u.UserId = ur.UserId
            WHERE u.pkid = @Pkid AND ur.RoleId = @RoleId
            """, new { Pkid = pkid, RoleId = roleId }, tx);

        if (row is null)
        {
            return false;
        }

        await conn.ExecuteAsync("DELETE FROM AppUserRole WHERE pkid = @Pkid", new { Pkid = row.Pkid }, tx);
        await auditWriter.LogDeleteAsync("AppUserRole", row, conn, tx);

        tx.Commit();
        return true;
    }

    private static async Task<AppUserRole?> LoadRoleRowAsync(IDbConnection conn, int pkid, IDbTransaction tx)
        => await conn.QuerySingleOrDefaultAsync<AppUserRole>(
            "SELECT pkid, UserId, RoleId FROM AppUserRole WHERE pkid = @Pkid", new { Pkid = pkid }, tx);

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
