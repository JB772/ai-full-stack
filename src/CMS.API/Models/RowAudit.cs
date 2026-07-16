namespace CMS.API.Models;

/// <summary>
/// 資料異動稽核 (RowAudit) 記錄。描述任一業務資料表的一次 Insert / Update / Delete。
/// 對應資料表 dbo.RowAudit；pkid 為 IDENTITY，由資料庫產生，寫入時不提供。
/// </summary>
public class RowAudit
{
    /// <summary>異動的資料表名稱 (例如 "Course")。</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>執行異動的登入者 UserName；無登入者時為 "system"。</summary>
    public string UserName { get; set; } = "system";

    /// <summary>異動資料列的主鍵值 (pkid) 字串。</summary>
    public string PrimaryKeyValues { get; set; } = string.Empty;

    /// <summary>異動類型："Insert" | "Update" | "Delete"。</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>異動描述 (最多 1000 字元)。Insert/Delete 為首個字串屬性值；Update 為異動的屬性名稱清單。</summary>
    public string? ActionDesc { get; set; }

    /// <summary>異動時間。</summary>
    public DateTime DateTime { get; set; }
}
