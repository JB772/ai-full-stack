using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 資料異動稽核 (RowAudit) 檢視 API。任一資料列的異動歷程皆可透過 TableName + 主鍵值查詢，
/// 供前端「異動紀錄 History」徽章 / 對話框使用。唯讀 —— 稽核列由各 Repository 於寫入時產生。
/// </summary>
[ApiController]
[Route("api/rowaudit")]
[Produces("application/json")]
public class RowAuditController(IRowAuditRepository repository) : ControllerBase
{
    /// <summary>
    /// 取得指定資料列的異動歷程，最新的在前。
    /// 例如 GET /api/rowaudit?tableName=Course&amp;pkid=123。
    /// </summary>
    /// <param name="tableName">資料表名稱 (例如 "Course")。</param>
    /// <param name="pkid">該資料列的主鍵值 (字串比對 dbo.RowAudit.PrimaryKeyValues)。</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RowAuditEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<RowAuditEntry>>> GetForRecord(
        [FromQuery] string? tableName, [FromQuery] string? pkid)
    {
        if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(pkid))
        {
            return BadRequest(new { message = "tableName 與 pkid 為必填。" });
        }

        return Ok(await repository.GetForRecordAsync(tableName.Trim(), pkid.Trim()));
    }
}
