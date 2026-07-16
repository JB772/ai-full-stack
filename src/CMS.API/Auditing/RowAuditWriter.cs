using System.Data;
using System.Reflection;
using CMS.API.Models;
using CMS.API.Security;
using Dapper;

namespace CMS.API.Auditing;

/// <summary>
/// 跨資料表的資料異動稽核寫入器。每次 Insert / Update / Delete 後由 Repository 呼叫，
/// 對 dbo.RowAudit 寫入「一列」描述此次異動的稽核記錄。以反射方式運作，適用於任意實體型別。
/// </summary>
/// <remarks>
/// 稽核列寫在「呼叫端提供的連線/交易」上 (Repository 傳入其開啟中的 IDbConnection 與 IDbTransaction)，
/// 因此若異動被回滾或失敗，稽核列不會留存 (同一交易一起 commit/rollback)。
/// UserName 取自目前請求 JWT 的 userName 宣告 (Lab 05 設定)；無登入者時為 "system"。
/// 反射邏輯 (首個字串屬性、變更屬性名稱、pkid) 皆為純運算的 static 方法，方便單元測試。
/// </remarks>
public class RowAuditWriter(IHttpContextAccessor httpContextAccessor)
{
    /// <summary>ActionDesc 欄位上限 (dbo.RowAudit.ActionDesc 為 varchar(1000))。</summary>
    public const int MaxActionDescLength = 1000;

    private const string InsertSql = """
        INSERT INTO RowAudit (TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc, [DateTime])
        VALUES (@TableName, @UserName, @PrimaryKeyValues, @ActionType, @ActionDesc, @DateTime);
        """;

    /// <summary>記錄一次新增：ActionDesc 取實體首個字串屬性值。寫在呼叫端的連線/交易上。</summary>
    public Task LogInsertAsync(string tableName, object entity, IDbConnection connection, IDbTransaction? transaction = null)
        => WriteAsync(BuildInsert(tableName, entity, CurrentUserName(), DateTime.Now), connection, transaction);

    /// <summary>
    /// 記錄一次修改：ActionDesc 為 before/after 之間值有變動的屬性名稱 (逗號分隔)。
    /// 若無任何屬性變動，則不寫入任何記錄。寫在呼叫端的連線/交易上。
    /// </summary>
    public Task LogUpdateAsync(string tableName, object before, object after, IDbConnection connection, IDbTransaction? transaction = null)
    {
        var entry = BuildUpdate(tableName, before, after, CurrentUserName(), DateTime.Now);
        // 無變更 → ActionDesc 為空 → 略過不寫入。
        return string.IsNullOrEmpty(entry.ActionDesc) ? Task.CompletedTask : WriteAsync(entry, connection, transaction);
    }

    /// <summary>記錄一次刪除：ActionDesc 取被刪除實體首個字串屬性值。寫在呼叫端的連線/交易上。</summary>
    public Task LogDeleteAsync(string tableName, object entity, IDbConnection connection, IDbTransaction? transaction = null)
        => WriteAsync(BuildDelete(tableName, entity, CurrentUserName(), DateTime.Now), connection, transaction);

    private static Task WriteAsync(RowAudit entry, IDbConnection connection, IDbTransaction? transaction)
        => connection.ExecuteAsync(InsertSql, entry, transaction);

    /// <summary>
    /// 目前請求的登入者 UserName，讀自 JWT 的 userName 宣告 (退而求其次讀 name 宣告)；
    /// 無已驗證使用者時回傳 "system"。
    /// </summary>
    public string CurrentUserName()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return "system";
        }

        var userName = user.FindFirst(JwtTokenService.UserNameClaimType)?.Value
            ?? user.FindFirst("name")?.Value;

        return string.IsNullOrWhiteSpace(userName) ? "system" : userName;
    }

    // ---- Pure reflection logic (no DB / no HttpContext) — unit-tested directly. ----

    /// <summary>組出一筆 Insert 稽核記錄。</summary>
    public static RowAudit BuildInsert(string tableName, object entity, string userName, DateTime now) => new()
    {
        TableName = tableName,
        UserName = userName,
        PrimaryKeyValues = FindPkidValue(entity),
        ActionType = "Insert",
        ActionDesc = Truncate(FirstStringPropertyValue(entity)),
        DateTime = now,
    };

    /// <summary>組出一筆 Delete 稽核記錄 (ActionDesc 規則同 Insert)。</summary>
    public static RowAudit BuildDelete(string tableName, object entity, string userName, DateTime now) => new()
    {
        TableName = tableName,
        UserName = userName,
        PrimaryKeyValues = FindPkidValue(entity),
        ActionType = "Delete",
        ActionDesc = Truncate(FirstStringPropertyValue(entity)),
        DateTime = now,
    };

    /// <summary>組出一筆 Update 稽核記錄 (ActionDesc 為變更屬性名稱清單)。pkid 取自 after。</summary>
    public static RowAudit BuildUpdate(string tableName, object before, object after, string userName, DateTime now) => new()
    {
        TableName = tableName,
        UserName = userName,
        PrimaryKeyValues = FindPkidValue(after),
        ActionType = "Update",
        ActionDesc = Truncate(ChangedPropertyNames(before, after)),
        DateTime = now,
    };

    /// <summary>以反射尋找 pkid 屬性 (不分大小寫) 並回傳其字串值；找不到時回傳空字串。</summary>
    public static string FindPkidValue(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var pkid = ReadableProperties(entity.GetType())
            .FirstOrDefault(p => p.Name.Equals("pkid", StringComparison.OrdinalIgnoreCase));
        return pkid?.GetValue(entity)?.ToString() ?? string.Empty;
    }

    /// <summary>回傳實體「宣告順序」中首個字串型別屬性的值；無字串屬性時回傳空字串。</summary>
    public static string FirstStringPropertyValue(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        var first = ReadableProperties(entity.GetType())
            .FirstOrDefault(p => p.PropertyType == typeof(string));
        return first?.GetValue(entity) as string ?? string.Empty;
    }

    /// <summary>
    /// 逐一比對 before/after 的每個「純量欄位」屬性，回傳值有變動的屬性名稱 (依宣告順序、逗號分隔)。
    /// 全部相同時回傳空字串。導覽物件 / 集合等非純量屬性 (例如 Course.Partner) 一律略過 —— 稽核追蹤的是
    /// 欄位值，且每次載入的導覽物件皆為不同實體，比對參考只會產生假變更。
    /// </summary>
    public static string ChangedPropertyNames(object before, object after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var changed = ReadableProperties(after.GetType())
            .Where(p => IsScalar(p.PropertyType))
            .Where(p => !Equals(p.GetValue(before), p.GetValue(after)))
            .Select(p => p.Name);

        return string.Join(", ", changed);
    }

    /// <summary>可讀取的公開執行個體屬性，依 MetadataToken 排序以還原宣告順序。</summary>
    private static IEnumerable<PropertyInfo> ReadableProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.MetadataToken);

    /// <summary>純量欄位型別 (對應資料庫欄位)：字串、原生型別、enum、decimal、日期時間、Guid 及其 Nullable。</summary>
    private static bool IsScalar(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(string)
            || t.IsPrimitive
            || t.IsEnum
            || t == typeof(decimal)
            || t == typeof(DateTime)
            || t == typeof(DateTimeOffset)
            || t == typeof(DateOnly)
            || t == typeof(TimeOnly)
            || t == typeof(TimeSpan)
            || t == typeof(Guid);
    }

    /// <summary>截斷至 ActionDesc 上限 (1000 字元)。</summary>
    public static string Truncate(string value)
        => value.Length <= MaxActionDescLength ? value : value[..MaxActionDescLength];
}
