using System.Data;
using CMS.API.Auditing;
using CMS.API.Data;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace CMS.API.Tests;

/// <summary>
/// Proves the AUD-01 retrofit on the REAL <see cref="AuthRepository"/>, driven against in-memory SQLite —
/// the same approach <see cref="PublishStatusRepositoryAuditTests"/> uses, and the only way to execute real
/// Dapper code here (CmsApiFactory swaps every repository out, so endpoint tests can never reach it).
/// Never touches the live SQL Server database.
///
/// Portability note: AuthRepository's password paths call <c>SYSDATETIME()</c>, which SQLite has no notion
/// of, so the factory below defines it as a SQLite function. Everything else in this repository's SQL is
/// portable and runs unmodified.
///
/// The security-critical property under test: <see cref="AppUser"/> — the projection AuthRepository audits —
/// has no PasswordHash property, so a password change can only ever audit as ActionDesc
/// "PasswordUpdatedTime". The hash must never reach dbo.RowAudit, which any authenticated user can read via
/// GET /api/rowaudit.
/// </summary>
public class AuthRepositoryAuditTests : IDisposable
{
    private const string DefaultPassword = "P@ssw0rd-default";
    private const string KnownHash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    private readonly SqliteConnection _keepAlive;
    private readonly IDbConnectionFactory _factory;
    private readonly AuthRepository _repo;

    public AuthRepositoryAuditTests()
    {
        (_keepAlive, _factory) = NewDatabase();
        _repo = new AuthRepository(_factory, NewWriter());
    }

    public void Dispose() => _keepAlive.Dispose();

    // ---- UpdateUserNameAsync ----

    [Fact]
    public async Task UpdateUserName_WritesUpdateAuditRow_NamingTheChangedColumn()
    {
        var user = await _repo.UpdateUserNameAsync("admin", "新的名字");

        Assert.NotNull(user);
        Assert.Equal("新的名字", user!.UserName);

        var audit = Assert.Single(ReadAudits(_factory));
        Assert.Equal("AppUser", audit.TableName);
        Assert.Equal("Update", audit.ActionType);
        Assert.Equal("UserName", audit.ActionDesc);   // changed column NAME, not its value
        Assert.Equal("1", audit.PrimaryKeyValues);    // AppUser.pkid
        Assert.Equal("system", audit.UserName);       // no HttpContext → documented fallback
    }

    [Fact]
    public async Task UpdateUserName_ForAnUnknownAccount_WritesNothing()
    {
        var user = await _repo.UpdateUserNameAsync("nobody", "無此人");

        Assert.Null(user);
        Assert.Empty(ReadAudits(_factory));
        Assert.Equal("系統管理員", CurrentUserName(_factory, "admin")); // untouched
    }

    [Fact]
    public async Task UpdateUserName_ToTheSameValue_WritesNoAuditRow()
    {
        // RowAuditWriter deliberately skips a no-op update (docs/row-audit.md).
        var user = await _repo.UpdateUserNameAsync("admin", "系統管理員");

        Assert.NotNull(user);
        Assert.Empty(ReadAudits(_factory));
    }

    // ---- UpdatePasswordAsync ----

    [Fact]
    public async Task UpdatePassword_WritesUpdateAuditRow_AndNeverTheHash()
    {
        var ok = await _repo.UpdatePasswordAsync("admin", KnownHash);

        Assert.True(ok);
        var audit = Assert.Single(ReadAudits(_factory));
        Assert.Equal("AppUser", audit.TableName);
        Assert.Equal("Update", audit.ActionType);
        // PasswordHash is absent from the AppUser projection, so the only visible change is the timestamp.
        Assert.Equal("PasswordUpdatedTime", audit.ActionDesc);
        Assert.Equal("1", audit.PrimaryKeyValues);

        // The load-bearing assertion: the hash must not appear anywhere in the audit row.
        Assert.DoesNotContain(KnownHash, string.Join("|", audit.TableName, audit.UserName,
            audit.PrimaryKeyValues, audit.ActionType, audit.ActionDesc));
        Assert.Equal(KnownHash, StoredHash(_factory, "admin")); // but it WAS written to AppUser
    }

    [Fact]
    public async Task UpdatePassword_ForAnUnknownAccount_WritesNothing()
    {
        var ok = await _repo.UpdatePasswordAsync("nobody", KnownHash);

        Assert.False(ok);
        Assert.Empty(ReadAudits(_factory));
    }

    // ---- ResetPasswordToDefaultAsync ----

    [Fact]
    public async Task ResetPasswordToDefault_WritesUpdateAuditRow_AndNeverTheHash()
    {
        var ok = await _repo.ResetPasswordToDefaultAsync("admin");

        Assert.True(ok);
        var audit = Assert.Single(ReadAudits(_factory));
        Assert.Equal("AppUser", audit.TableName);
        Assert.Equal("Update", audit.ActionType);
        Assert.Equal("PasswordUpdatedTime", audit.ActionDesc);
        Assert.Equal("1", audit.PrimaryKeyValues);

        var expected = PasswordHasher.Hash(DefaultPassword);
        Assert.Equal(expected, StoredHash(_factory, "admin"));
        Assert.DoesNotContain(expected, audit.ActionDesc ?? "");
    }

    [Fact]
    public async Task ResetPasswordToDefault_ForAnUnknownAccount_WritesNothing()
    {
        var ok = await _repo.ResetPasswordToDefaultAsync("nobody");

        Assert.False(ok);
        Assert.Empty(ReadAudits(_factory));
    }

    /// <summary>
    /// The whole point of auditing on the caller's transaction: if the audit insert fails, the data change
    /// must roll back with it. Dropping RowAudit makes the audit write throw mid-transaction.
    /// </summary>
    [Fact]
    public async Task WhenTheAuditWriteFails_ThePasswordChangeRollsBack()
    {
        using var conn = _factory.CreateConnection();
        conn.Execute("DROP TABLE RowAudit");

        await Assert.ThrowsAnyAsync<Exception>(() => _repo.UpdatePasswordAsync("admin", KnownHash));

        // The UPDATE shared the failed transaction, so the old hash must survive.
        Assert.Equal("SEEDHASH", StoredHash(_factory, "admin"));
    }

    // ---- helpers ----

    private static RowAuditWriter NewWriter() => new(new HttpContextAccessor { HttpContext = null });

    private sealed record AuditRow(string TableName, string UserName, string PrimaryKeyValues, string ActionType, string? ActionDesc);

    private static IReadOnlyList<AuditRow> ReadAudits(IDbConnectionFactory factory)
    {
        using var conn = factory.CreateConnection();
        return conn.Query<AuditRow>(
            "SELECT TableName, UserName, PrimaryKeyValues, ActionType, ActionDesc FROM RowAudit ORDER BY pkid").ToList();
    }

    private static string StoredHash(IDbConnectionFactory factory, string userId)
    {
        using var conn = factory.CreateConnection();
        return conn.ExecuteScalar<string>("SELECT PasswordHash FROM AppUser WHERE UserId = @UserId", new { UserId = userId })!;
    }

    private static string CurrentUserName(IDbConnectionFactory factory, string userId)
    {
        using var conn = factory.CreateConnection();
        return conn.ExecuteScalar<string>("SELECT UserName FROM AppUser WHERE UserId = @UserId", new { UserId = userId })!;
    }

    private static (SqliteConnection keepAlive, IDbConnectionFactory factory) NewDatabase()
    {
        var connString = $"Data Source=authaudit_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        var keepAlive = new SqliteConnection(connString);
        keepAlive.Open();

        keepAlive.Execute("""
            CREATE TABLE AppUser (
                pkid INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                UserName TEXT NOT NULL,
                PasswordHash TEXT NULL,
                PasswordUpdatedTime TEXT NULL,
                IsActive INTEGER NOT NULL
            );
            CREATE TABLE AppUserRole (pkid INTEGER PRIMARY KEY AUTOINCREMENT, UserId TEXT NOT NULL, RoleId TEXT NOT NULL);
            CREATE TABLE SysConfig (configKey TEXT NOT NULL, configValue TEXT NOT NULL);
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

        keepAlive.Execute(
            "INSERT INTO AppUser (UserId, UserName, PasswordHash, PasswordUpdatedTime, IsActive) VALUES ('admin', '系統管理員', 'SEEDHASH', NULL, 1)");
        keepAlive.Execute("INSERT INTO AppUserRole (UserId, RoleId) VALUES ('admin', 'Admin')");
        keepAlive.Execute("INSERT INTO SysConfig (configKey, configValue) VALUES ('appConfig', @Json)",
            new { Json = $$"""{"defaultPassword":"{{DefaultPassword}}","symmetricSecurityKey":"unused-in-these-tests-but-present"}""" });

        return (keepAlive, new SqliteConnectionFactory(connString));
    }

    /// <summary>
    /// Hands out connections that understand <c>SYSDATETIME()</c>. AuthRepository's password paths use it
    /// (server-local time, deliberately — see the FE-01 finding); SQLite has no such function, so it is
    /// defined per connection here. This is the only concession the real SQL needs to run under SQLite.
    /// </summary>
    private sealed class SqliteConnectionFactory(string connectionString) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection()
        {
            var conn = new SqliteConnection(connectionString);
            conn.CreateFunction("SYSDATETIME", () => DateTime.Now.ToString("O"));
            return conn;
        }
    }
}
