using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

/// <summary>
/// 唯讀存取 dbo.RowAudit —— 供異動歷程檢視使用。寫入由 <see cref="Auditing.RowAuditWriter"/> 負責，
/// 本 Repository 只查詢。依 pkid 遞減排序 (稽核列的 pkid 為單調遞增的 IDENTITY，等同插入 / 時間順序)，
/// 因此「最新的在前」的排序不受同一毫秒多筆 DateTime 相同的影響。
/// </summary>
public class RowAuditRepository(IDbConnectionFactory connectionFactory) : IRowAuditRepository
{
    private const string SelectForRecord = """
        SELECT [DateTime], UserName, ActionType, ActionDesc
        FROM RowAudit
        WHERE TableName = @TableName AND PrimaryKeyValues = @PrimaryKeyValue
        ORDER BY pkid DESC
        """;

    public async Task<IEnumerable<RowAuditEntry>> GetForRecordAsync(string tableName, string primaryKeyValue)
    {
        using var conn = connectionFactory.CreateConnection();
        return await conn.QueryAsync<RowAuditEntry>(
            SelectForRecord, new { TableName = tableName, PrimaryKeyValue = primaryKeyValue });
    }
}
