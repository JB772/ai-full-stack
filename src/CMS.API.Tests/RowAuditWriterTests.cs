using System.Security.Claims;
using CMS.API.Auditing;
using CMS.API.Security;
using Microsoft.AspNetCore.Http;

namespace CMS.API.Tests;

/// <summary>
/// Unit tests for <see cref="RowAuditWriter"/>'s reflection logic. These exercise the pure,
/// DB-free build path — no SQL Server, no WebApplicationFactory.
/// </summary>
public class RowAuditWriterTests
{
    private static readonly DateTime Now = new(2026, 7, 15, 9, 30, 0);

    /// <summary>Name is the first string property (declaration order), so it is the Insert/Delete ActionDesc.</summary>
    private class Sample
    {
        public int Pkid { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }

    // ---- ActionDesc: first string property (Insert / Delete) ----

    [Fact]
    public void BuildInsert_UsesFirstStringProperty_AsActionDesc()
    {
        var entry = RowAuditWriter.BuildInsert(
            "Course", new Sample { Pkid = 1, Name = "Intro", Code = "C1" }, "alice", Now);

        Assert.Equal("Course", entry.TableName);
        Assert.Equal("Insert", entry.ActionType);
        Assert.Equal("Intro", entry.ActionDesc);
        Assert.Equal("alice", entry.UserName);
        Assert.Equal(Now, entry.DateTime);
    }

    [Fact]
    public void BuildDelete_UsesFirstStringProperty_AsActionDesc()
    {
        var entry = RowAuditWriter.BuildDelete(
            "Course", new Sample { Pkid = 1, Name = "Intro", Code = "C1" }, "alice", Now);

        Assert.Equal("Delete", entry.ActionType);
        Assert.Equal("Intro", entry.ActionDesc);
    }

    [Fact]
    public void FirstStringPropertyValue_ReturnsEmpty_WhenNoStringProperty()
    {
        Assert.Equal(string.Empty, RowAuditWriter.FirstStringPropertyValue(new { Pkid = 3, Count = 7 }));
    }

    // ---- ActionDesc: changed property names (Update) ----

    [Fact]
    public void BuildUpdate_ListsExactlyTheChangedPropertyNames_CommaSeparated()
    {
        var before = new Sample { Pkid = 5, Name = "A", Code = "X", DisplayOrder = 1 };
        var after = new Sample { Pkid = 5, Name = "B", Code = "X", DisplayOrder = 2 };

        var entry = RowAuditWriter.BuildUpdate("Course", before, after, "alice", Now);

        Assert.Equal("Update", entry.ActionType);
        Assert.Equal("Name, DisplayOrder", entry.ActionDesc);
    }

    [Fact]
    public void BuildUpdate_ProducesEmptyActionDesc_WhenNothingChanged()
    {
        var before = new Sample { Pkid = 5, Name = "A", Code = "X", DisplayOrder = 1 };
        var after = new Sample { Pkid = 5, Name = "A", Code = "X", DisplayOrder = 1 };

        var entry = RowAuditWriter.BuildUpdate("Course", before, after, "alice", Now);

        Assert.Equal(string.Empty, entry.ActionDesc);
    }

    [Fact]
    public void ChangedPropertyNames_ReturnsEmpty_WhenIdentical()
    {
        var before = new Sample { Pkid = 5, Name = "A" };
        var after = new Sample { Pkid = 5, Name = "A" };

        Assert.Equal(string.Empty, RowAuditWriter.ChangedPropertyNames(before, after));
    }

    // ---- PrimaryKeyValues: pkid via reflection ----

    [Fact]
    public void BuildInsert_ReadsPkid_AsPrimaryKeyValues()
    {
        var entry = RowAuditWriter.BuildInsert("Course", new Sample { Pkid = 42, Name = "x" }, "alice", Now);

        Assert.Equal("42", entry.PrimaryKeyValues);
    }

    [Fact]
    public void BuildUpdate_ReadsPkid_FromAfterEntity()
    {
        var before = new Sample { Pkid = 7, Name = "A" };
        var after = new Sample { Pkid = 7, Name = "B" };

        var entry = RowAuditWriter.BuildUpdate("Course", before, after, "alice", Now);

        Assert.Equal("7", entry.PrimaryKeyValues);
    }

    [Fact]
    public void FindPkidValue_IsCaseInsensitive_AndReturnsEmpty_WhenAbsent()
    {
        Assert.Equal("9", RowAuditWriter.FindPkidValue(new { PKID = 9 }));
        Assert.Equal(string.Empty, RowAuditWriter.FindPkidValue(new { Name = "no key here" }));
    }

    // ---- UserName: JWT claim, "system" fallback ----

    [Fact]
    public void CurrentUserName_FallsBackToSystem_WhenNoAuthenticatedUser()
    {
        var writer = new RowAuditWriter(new HttpContextAccessor { HttpContext = null });

        Assert.Equal("system", writer.CurrentUserName());
    }

    [Fact]
    public void CurrentUserName_FallsBackToSystem_WhenIdentityNotAuthenticated()
    {
        // A ClaimsIdentity with no authentication type is unauthenticated even if it carries claims.
        var identity = new ClaimsIdentity([new Claim(JwtTokenService.UserNameClaimType, "ghost")]);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        var writer = new RowAuditWriter(accessor);

        Assert.Equal("system", writer.CurrentUserName());
    }

    [Fact]
    public void CurrentUserName_ReadsUserNameClaim_WhenAuthenticated()
    {
        var identity = new ClaimsIdentity([new Claim(JwtTokenService.UserNameClaimType, "系統管理員")], "TestAuth");
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        var writer = new RowAuditWriter(accessor);

        Assert.Equal("系統管理員", writer.CurrentUserName());
    }

    // ---- ActionDesc truncation at 1000 characters ----

    [Fact]
    public void BuildInsert_TruncatesActionDesc_At1000Characters()
    {
        var longName = new string('x', 1500);

        var entry = RowAuditWriter.BuildInsert("Course", new Sample { Pkid = 1, Name = longName }, "alice", Now);

        Assert.Equal(RowAuditWriter.MaxActionDescLength, entry.ActionDesc!.Length);
        Assert.Equal(1000, entry.ActionDesc.Length);
    }

    [Fact]
    public void Truncate_LeavesShortStringsUnchanged()
    {
        Assert.Equal("short", RowAuditWriter.Truncate("short"));
    }

    /// <summary>Nav objects (class-typed props) are ignored — only scalar column changes are reported.</summary>
    [Fact]
    public void ChangedPropertyNames_IgnoresNavObjectProperties()
    {
        var before = new WithNav { Pkid = 1, Title = "A", Nav = new Nav { Name = "x" } };
        var after = new WithNav { Pkid = 1, Title = "A", Nav = new Nav { Name = "y" } }; // different Nav instance & value

        // Title unchanged and Nav is non-scalar → nothing reported.
        Assert.Equal(string.Empty, RowAuditWriter.ChangedPropertyNames(before, after));
    }

    private class WithNav
    {
        public int Pkid { get; set; }
        public string Title { get; set; } = string.Empty;
        public Nav? Nav { get; set; }
    }

    private class Nav
    {
        public string Name { get; set; } = string.Empty;
    }
}
