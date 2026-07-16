using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IRowAuditRepository
{
    /// <summary>
    /// 取得某一資料列 (TableName + 主鍵值) 的完整異動歷程，最新的在前。
    /// </summary>
    Task<IEnumerable<RowAuditEntry>> GetForRecordAsync(string tableName, string primaryKeyValue);
}
