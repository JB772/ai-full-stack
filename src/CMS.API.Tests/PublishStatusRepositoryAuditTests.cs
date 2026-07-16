using System.Data;
using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Repositories;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace CMS.API.Tests;

/// <summary>
/// Proves the RowAudit retrofit end-to-end on a real Dapper repository, driven against an in-memory
/// SQLite database. PublishStatus is the chosen repository because its SQL is portable (no SCOPE_IDENTITY
/// / nchar), so the real production code runs unmodified. Never touches the live SQL Server database.
/// </summary>
public class PublishStatusRepositoryAuditTests : IDisposable
{
    private readonly SqliteConnection _keepAlive;
    private readonly IDbConnectionFactory _factory;
    private readonly PublishStatusRepository _repo;

    public PublishStatusRepositoryAuditTests()
    {
        (_keepAlive, _factory) = NewDatabase(withRowAudit: true);
        _repo = new PublishStatusRepository(_factory, NewWriter());
    }

    public void Dispose() => _keepAlive.Dispose();

    // ---- Insert ----

    [Fact]
    public async Task Create_WritesInsertAuditRow_WithFirstStringColumn()
    {
        var pkid = await _repo.CreateAsync(new PublishStatusRequest { Pkid = 5, Description = "草稿狀態", IsDraft = true });

        Assert.Equal((byte)5, pkid);
        var audit = Assert.Single(ReadAudits(_factory));
        Assert.Equal("PublishStatus", audit.TableName);
        Assert.Equal("Insert", audit.ActionType);
        Assert.Equal("草稿狀態", audit.ActionDesc);      // Description = first string column
        Assert.Equal("5", audit.PrimaryKeyValues);
        Assert.Equal("system", audit.UserName);          // no HttpContext → fallback
    }

    // ---- Update ----

    [Fact]
    public async Task Update_WritesUpdateAuditRow_ListingExactlyTheChangedColumns()
    {
        await _repo.CreateAsync(new PublishStatusRequest
        {
            Pkid = 7, Description = "原說明", IsDraft = true, IsPublished = false, IsDiscontinued = false
        });
        ClearAudits(_factory); // drop the Insert audit so only the Update remains

        var ok = await _repo.UpdateAsync(new PublishStatusRequest
        {
            Pkid = 7, Description = "新說明", IsDraft = false, IsPublished = true, IsDiscontinued = false
        });

        Assert.True(ok);
        var audit = Assert.Single(ReadAudits(_factory));
        Assert.Equal("Update", audit.ActionType);
        Assert.Equal("7", audit.PrimaryKeyValues);
        // Description, IsDraft, IsPublished changed; IsDiscontinued (and the counts / pkid) did not.
        Assert.Equal("Description, IsDraft, IsPublished", audit.ActionDesc);
    }

    [Fact]
    public async Task Update_WithNoActualChange_WritesNoAuditRow()
    {
        await _repo.CreateAsync(new PublishStatusRequest { Pkid = 8, Description = "不變", IsDraft = true });
        ClearAudits(_factory);

        var ok = await _repo.UpdateAsync(new PublishStatusRequest { Pkid = 8, Description = "不變", IsDraft = true });

        Assert.True(ok);                    // the row exists, so the update itself "succeeds"
        Assert.Empty(ReadAudits(_factory)); // but nothing changed → no audit row
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_WritesDeleteAuditRow_WithFirstStringColumn()
    {
        await _repo.CreateAsync(new PublishStatusRequest { Pkid = 9, Description = "待刪除", IsDiscontinued = true });
        ClearAudits(_factory);

        var ok = await _repo.DeleteAsync(9);

        Assert.True(ok);
        var audit = Assert.Single(ReadAudits(_factory));
        Assert.Equal("Delete", audit.ActionType);
        Assert.Equal("待刪除", audit.ActionDesc);
        Assert.Equal("9", audit.PrimaryKeyValues);
    }

    // ---- A failed change leaves no audit row ----

    [Fact]
    public async Task Create_ThatFails_LeavesNoAuditRow()
    {
        await _repo.CreateAsync(new PublishStatusRequest { Pkid = 3, Description = "原本" });
        ClearAudits(_factory);

        // Re-inserting the same pkid violates the primary key → the operation throws before commit.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _repo.CreateAsync(new PublishStatusRequest { Pkid = 3, Description = "重複" }));

        Assert.Empty(ReadAudits(_factory));         // no audit row for the failed change
        var still = await _repo.GetByPkidAsync(3);
        Assert.Equal("原本", still!.Description);    // original row untouched
    }

    [Fact]
    public async Task Create_WhenAuditInsertFails_RollsBackTheOperationToo()
    {
        // A database WITHOUT the RowAudit table: the operation INSERT succeeds, then the audit INSERT
        // throws. Because both run in one transaction, the operation must roll back — proving the audit
        // shares the operation's transaction (there is no committed change without its audit row).
        var (keepAlive, factory) = NewDatabase(withRowAudit: false);
        using var _ = keepAlive;
        var repo = new PublishStatusRepository(factory, NewWriter());

        await Assert.ThrowsAnyAsync<Exception>(() =>
            repo.CreateAsync(new PublishStatusRequest { Pkid = 4, Description = "應被回滾" }));

        Assert.Null(await repo.GetByPkidAsync(4)); // rolled back together with the failed audit insert
    }

    // ---- infrastructure ----

    private static RowAuditWriter NewWriter() => new(new HttpContextAccessor { HttpContext = null });

    private sealed record AuditRow(string TableName, string UserName, string PrimaryKeyValues, string ActionType, string? ActionDesc);

    private static IReadOnlyList<AuditRow> ReadAudits(IDbConnectionFactory factory)
    {
        using var conn = factory.CreateConnection();
        return conn.Query<AuditRow>(
            "SELECT TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc FROM RowAudit ORDER BY pkid").ToList();
    }

    private static void ClearAudits(IDbConnectionFactory factory)
    {
        using var conn = factory.CreateConnection();
        conn.Execute("DELETE FROM RowAudit");
    }

    /// <summary>
    /// A fresh, uniquely-named shared in-memory SQLite database. The returned keep-alive connection must
    /// stay open for the test's lifetime — the in-memory DB is destroyed when the last connection closes.
    /// </summary>
    private static (SqliteConnection keepAlive, IDbConnectionFactory factory) NewDatabase(bool withRowAudit)
    {
        var connString = $"Data Source=psaudit_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var keepAlive = new SqliteConnection(connString);
        keepAlive.Open();

        keepAlive.Execute("""
            CREATE TABLE PublishStatus (
                pkid INTEGER PRIMARY KEY,
                Description TEXT NOT NULL,
                IsDraft INTEGER NOT NULL,
                IsPublished INTEGER NOT NULL,
                IsDiscontinued INTEGER NOT NULL
            );
            CREATE TABLE Course (PublishStatus_pkid INTEGER);
            CREATE TABLE Promotion2 (PublishStatus_pkid INTEGER);
            """);

        if (withRowAudit)
        {
            keepAlive.Execute("""
                CREATE TABLE RowAudit (
                    pkid INTEGER PRIMARY KEY AUTOINCREMENT,
                    TableName TEXT NOT NULL,
                    UserName TEXT NOT NULL,
                    PrimaryKeyValues TEXT NOT NULL,
                    ActionType TEXT NOT NULL,
                    ActionDesc TEXT NULL,
                    [DateTime] TEXT NOT NULL
                );
                """);
        }

        return (keepAlive, new SqliteConnectionFactory(connString));
    }

    private sealed class SqliteConnectionFactory(string connectionString) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => new SqliteConnection(connectionString);
    }
}
