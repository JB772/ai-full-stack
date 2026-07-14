using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class AppRoleRepository(IDbConnectionFactory connectionFactory) : IAppRoleRepository
{
    private const string SelectColumns = """
        SELECT r.pkid, r.RoleId, r.RoleName, r.PermissionLevel, r.Description,
               (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.RoleId = r.RoleId) AS UserCount
        FROM AppRole r
        """;

    public async Task<IEnumerable<AppRole>> GetAllAsync()
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<AppRole>($"{SelectColumns} ORDER BY r.RoleId ASC");
    }

    public async Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("(r.RoleId LIKE @Keyword OR r.RoleName LIKE @Keyword OR r.Description LIKE @Keyword)");
            parameters.Add("Keyword", $"%{query.Keyword.Trim()}%");
        }

        if (query.PermissionLevelFrom.HasValue)
        {
            where.Add("r.PermissionLevel >= @PermissionLevelFrom");
            parameters.Add("PermissionLevelFrom", query.PermissionLevelFrom.Value);
        }

        if (query.PermissionLevelTo.HasValue)
        {
            where.Add("r.PermissionLevel <= @PermissionLevelTo");
            parameters.Add("PermissionLevelTo", query.PermissionLevelTo.Value);
        }

        var sql = SelectColumns
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
            + " ORDER BY r.RoleId ASC";

        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<AppRole>(sql, parameters);
    }

    public async Task<AppRole?> GetByPkidAsync(int pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AppRole>(
            $"{SelectColumns} WHERE r.pkid = @Pkid", new { Pkid = pkid });
    }

    public async Task<bool> RoleIdExistsAsync(string roleId, int? excludePkid = null)
    {
        const string sql = """
            SELECT COUNT(1) FROM AppRole
            WHERE RoleId = @RoleId AND (@ExcludePkid IS NULL OR pkid <> @ExcludePkid)
            """;

        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql, new { RoleId = roleId, ExcludePkid = excludePkid }) > 0;
    }

    public async Task<int> CreateAsync(AppRoleRequest request)
    {
        const string sql = """
            INSERT INTO AppRole (RoleId, RoleName, PermissionLevel, Description)
            VALUES (@RoleId, @RoleName, @PermissionLevel, @Description);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql, request);
    }

    /// <summary>RoleId is the natural key referenced by AppUserRole, so it is not updatable.</summary>
    public async Task<bool> UpdateAsync(AppRoleRequest request)
    {
        const string sql = """
            UPDATE AppRole
            SET RoleName = @RoleName,
                PermissionLevel = @PermissionLevel,
                Description = @Description
            WHERE pkid = @Pkid;
            """;

        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteAsync(sql, request) > 0;
    }

    public async Task<bool> DeleteAsync(int pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteAsync("DELETE FROM AppRole WHERE pkid = @Pkid", new { Pkid = pkid }) > 0;
    }

    public async Task<int> GetUserCountAsync(int pkid)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM AppUserRole ur
            INNER JOIN AppRole r ON r.RoleId = ur.RoleId
            WHERE r.pkid = @Pkid
            """;

        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(sql, new { Pkid = pkid });
    }
}
