namespace CMS.API.Models;

/// <summary>
/// 單筆資料異動稽核記錄的「檢視」投影 —— 用於某一資料列的異動歷程 API (GET /api/rowaudit)。
/// 僅回傳前端顯示所需的四個欄位；不含 TableName / PrimaryKeyValues (查詢條件本身已知)。
/// </summary>
public class RowAuditEntry
{
    /// <summary>異動時間。</summary>
    public DateTime DateTime { get; set; }

    /// <summary>執行異動的登入者 UserName；無登入者時為 "system"。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>異動類型："Insert" | "Update" | "Delete"。</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>異動描述 (Insert/Delete 為首個字串欄位值；Update 為異動的欄位名稱清單)。</summary>
    public string? ActionDesc { get; set; }
}
