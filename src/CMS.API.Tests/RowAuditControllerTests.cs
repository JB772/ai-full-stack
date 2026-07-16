using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for GET /api/rowaudit. Driven through the in-memory <c>InMemoryRowAuditRepository</c>
/// (swapped in by <see cref="CmsApiFactory"/>), which is seeded with audit rows across several tables
/// and pkids so these tests prove the filter and the newest-first ordering.
/// </summary>
public class RowAuditControllerTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _client;

    public RowAuditControllerTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetForRecord_ReturnsOnlyRowsForThatTableAndPkid()
    {
        var entries = await _client.GetFromJsonAsync<List<RowAuditEntry>>("/api/rowaudit?tableName=Course&pkid=123");

        Assert.NotNull(entries);
        // The seed has 3 rows for Course/123, plus a Partner/123 and a Course/999 that must be excluded.
        Assert.Equal(3, entries!.Count);
        Assert.All(entries, e => Assert.Contains(e.ActionType, new[] { "Insert", "Update" }));
        Assert.DoesNotContain(entries, e => e.UserName == "carol"); // Partner/123
        Assert.DoesNotContain(entries, e => e.UserName == "dave");  // Course/999
    }

    [Fact]
    public async Task GetForRecord_ReturnsRowsNewestFirst()
    {
        var entries = await _client.GetFromJsonAsync<List<RowAuditEntry>>("/api/rowaudit?tableName=Course&pkid=123");

        Assert.NotNull(entries);
        // Seed dates: Insert 2026-01-10, Update 2026-03-20, Update 2026-06-04 → newest first.
        var dates = entries!.Select(e => e.DateTime).ToList();
        Assert.Equal(dates.OrderByDescending(d => d).ToList(), dates);
        Assert.Equal(new DateTime(2026, 6, 4, 14, 30, 0), entries[0].DateTime);
        Assert.Equal("alice", entries[0].UserName);
        Assert.Equal("Update", entries[0].ActionType);
        Assert.Equal(new DateTime(2026, 1, 10, 9, 0, 0), entries[^1].DateTime);
        Assert.Equal("Insert", entries[^1].ActionType);
    }

    [Fact]
    public async Task GetForRecord_ReturnsAllFourFields()
    {
        var entries = await _client.GetFromJsonAsync<List<RowAuditEntry>>("/api/rowaudit?tableName=Course&pkid=123");

        var latest = entries![0];
        Assert.Equal(new DateTime(2026, 6, 4, 14, 30, 0), latest.DateTime);
        Assert.Equal("alice", latest.UserName);
        Assert.Equal("Update", latest.ActionType);
        Assert.Equal("Title, ListPrice", latest.ActionDesc);
    }

    [Fact]
    public async Task GetForRecord_WithNoHistory_ReturnsEmptyList()
    {
        var entries = await _client.GetFromJsonAsync<List<RowAuditEntry>>("/api/rowaudit?tableName=Course&pkid=55555");

        Assert.NotNull(entries);
        Assert.Empty(entries!);
    }

    [Fact]
    public async Task GetForRecord_MissingTableName_Returns400()
    {
        var response = await _client.GetAsync("/api/rowaudit?pkid=123");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetForRecord_MissingPkid_Returns400()
    {
        var response = await _client.GetAsync("/api/rowaudit?tableName=Course");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetForRecord_WithoutToken_Returns401()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/rowaudit?tableName=Course&pkid=123");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
