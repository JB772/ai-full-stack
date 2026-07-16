using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Stands in for <see cref="RowAuditRepository"/> so the /api/rowaudit endpoint can be exercised
/// without SQL Server. Seeded with audit rows spanning several tables and pkids so tests can prove
/// the filter (TableName + PrimaryKeyValues) and the newest-first ordering.
/// </summary>
public class InMemoryRowAuditRepository : IRowAuditRepository
{
    private sealed record Row(string TableName, string PrimaryKeyValues, RowAuditEntry Entry);

    // Deliberately stored out of chronological order so a passing test must actually sort.
    private readonly List<Row> _rows =
    [
        // Course / 123 — three changes for the same record (this is what the happy-path test filters on).
        new("Course", "123", new RowAuditEntry
        {
            DateTime = new DateTime(2026, 6, 4, 14, 30, 0),
            UserName = "alice", ActionType = "Update", ActionDesc = "Title, ListPrice"
        }),
        new("Course", "123", new RowAuditEntry
        {
            DateTime = new DateTime(2026, 1, 10, 9, 0, 0),
            UserName = "system", ActionType = "Insert", ActionDesc = "初版課程"
        }),
        new("Course", "123", new RowAuditEntry
        {
            DateTime = new DateTime(2026, 3, 20, 11, 15, 0),
            UserName = "bob", ActionType = "Update", ActionDesc = "PublishStatus_pkid"
        }),

        // Same pkid, DIFFERENT table — must be excluded when filtering Course/123.
        new("Partner", "123", new RowAuditEntry
        {
            DateTime = new DateTime(2026, 5, 1, 8, 0, 0),
            UserName = "carol", ActionType = "Update", ActionDesc = "Name"
        }),

        // Same table, DIFFERENT pkid — must be excluded when filtering Course/123.
        new("Course", "999", new RowAuditEntry
        {
            DateTime = new DateTime(2026, 7, 1, 8, 0, 0),
            UserName = "dave", ActionType = "Delete", ActionDesc = "舊課程"
        }),
    ];

    public Task<IEnumerable<RowAuditEntry>> GetForRecordAsync(string tableName, string primaryKeyValue)
    {
        var result = _rows
            .Where(r => r.TableName == tableName && r.PrimaryKeyValues == primaryKeyValue)
            .OrderByDescending(r => r.Entry.DateTime) // newest first
            .Select(r => r.Entry)
            .ToList();

        return Task.FromResult<IEnumerable<RowAuditEntry>>(result);
    }
}
