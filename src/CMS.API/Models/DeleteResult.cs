namespace CMS.API.Models;

/// <summary>
/// DeleteAsync 的結果：找不到資料列、被子資料擋下 (409)、或成功刪除。
///
/// 為什麼需要 <see cref="Blocked"/>：控制器的刪除前檢查跑在另一條連線上，和實際 DELETE 不同交易。
/// 對 <c>ON DELETE CASCADE</c> 的外鍵而言 SQL Server 「不會」擋下刪除 (見 docs/delete-guards.md)，
/// 所以在那個空窗期新增的子資料會被無聲刪除。Repository 必須在自己的交易內「再檢查一次」，
/// 而它需要一個和「找不到」不同的回傳值來表達「被擋下」。
/// </summary>
public enum DeleteResult
{
    NotFound,
    Blocked,
    Deleted
}
