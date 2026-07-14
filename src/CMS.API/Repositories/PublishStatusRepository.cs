using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class PublishStatusRepository(IDbConnectionFactory connectionFactory) : IPublishStatusRepository
{
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
        await conn.ExecuteAsync(sql, request);
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
        return await conn.ExecuteAsync(sql, request) > 0;
    }

    public async Task<bool> DeleteAsync(byte pkid)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.ExecuteAsync(
            "DELETE FROM PublishStatus WHERE pkid = @Pkid", new { Pkid = pkid }) > 0;
    }
}
