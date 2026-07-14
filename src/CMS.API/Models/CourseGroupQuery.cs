namespace CMS.API.Models;

/// <summary>
/// 課程群組 (CourseGroup) 查詢條件。
/// 只有關鍵字一項：本表除了主代碼只有 Description 一個欄位，沒有外鍵、布林或日期欄位可篩選。
/// </summary>
public class CourseGroupQuery
{
    /// <summary>關鍵字：比對 Description</summary>
    public string? Keyword { get; set; }
}
