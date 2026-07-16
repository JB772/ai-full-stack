using System.Data;
using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class PublishStatusRepository(IDbConnectionFactory connectionFactory, RowAuditWriter auditWriter) : IPublishStatusRepository
{
    private const string TableName = "PublishStatus";

    private const string SelectColumns = """
        SELECT p.pkid, p.Description, p.IsDraft, p.IsPublished, p.IsDiscontinued,
               (SELECT COUNT(*) FROM Course c     WHERE c.PublishStatus_pkid = p.pkid) AS CourseCount,
               (SELECT COUNT(*) FROM Promotion2 m WHERE m.PublishStatus_pkid = p.pkid) AS PromotionCount
        FROM PublishStatus p
        """;

    public async Task<IEnumerable<PublishStatus>> GetAllAsync()
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<PublishStatus>($"{SelectColumns} ORDER BY p.pkid ASC");
    }

    public async Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            where.Add("p.Description LIKE @Keyword");
            parameters.Add("Keyword", $"%{query.Keyword.Trim()}%");
        }

        if (query.IsDraft.HasValue)
        {
            where.Add("p.IsDraft = @IsDraft");
            parameters.Add("IsDraft", query.IsDraft.Value);
        }

        if (query.IsPublished.HasValue)
        {
            where.Add("p.IsPublished = @IsPublished");
            parameters.Add("IsPublished", query.IsPublished.Value);
        }

        if (query.IsDiscontinued.HasValue)
        {
            where.Add("p.IsDiscontinued = @IsDiscontinued");
            parameters.Add("IsDiscontinued", query.IsDiscontinued.Value);
        }

        var sql = SelectColumns
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
            + " ORDER BY p.pkid ASC";

        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<PublishStatus>(sql, parameters);
    }

    public async Task<PublishStatus?> GetByPkidAsync(byte pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<PublishStatus>(
            $"{SelectColumns} WHERE p.pkid = @Pkid", new { Pkid = pkid });
    }

    public async Task<bool> PkidExistsAsync(byte pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM PublishStatus WHERE pkid = @Pkid", new { Pkid = pkid }) > 0;
    }

    /// <summary>pkid is not an IDENTITY column, so it is inserted explicitly and echoed back.</summary>
    public async Task<byte> CreateAsync(PublishStatusRequest request)
    {
        const string sql = """
            INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
            VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
            """;

        using var conn = connectionFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(sql, request, tx);
        var created = await LoadByPkidAsync(conn, request.Pkid, tx);
        await auditWriter.LogInsertAsync(TableName, created!, conn, tx);

        tx.Commit();
        return request.Pkid;
    }

    /// <summary>pkid identifies the row and is never re-written.</summary>
    public async Task<bool> UpdateAsync(PublishStatusRequest request)
    {
        const string sql = """
            UPDATE PublishStatus
            SET Description = @Description,
                IsDraft = @IsDraft,
                IsPublished = @IsPublished,
                IsDiscontinued = @IsDiscontinued
            WHERE pkid = @Pkid;
            """;

        using var conn = connectionFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        // Load the "before" first so the audit can compare it against the post-update "after".
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

    public async Task<bool> DeleteAsync(byte pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction();

        // Load the row first so its first string column is available for the audit's ActionDesc.
        var row = await LoadByPkidAsync(conn, pkid, tx);
        if (row is null)
        {
            return false;
        }

        await conn.ExecuteAsync("DELETE FROM PublishStatus WHERE pkid = @Pkid", new { Pkid = pkid }, tx);
        await auditWriter.LogDeleteAsync(TableName, row, conn, tx);

        tx.Commit();
        return true;
    }

    /// <summary>Loads a row on an existing open connection/transaction — used inside the write transactions
    /// so the audit sees the same in-flight state (a standalone GetByPkidAsync would open its own connection
    /// and could not see the uncommitted change).</summary>
    private static async Task<PublishStatus?> LoadByPkidAsync(IDbConnection conn, byte pkid, IDbTransaction tx)
        => await conn.QuerySingleOrDefaultAsync<PublishStatus>(
            $"{SelectColumns} WHERE p.pkid = @Pkid", new { Pkid = pkid }, tx);
}
