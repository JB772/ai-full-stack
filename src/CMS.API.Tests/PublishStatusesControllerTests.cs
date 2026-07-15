using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for /api/publish-statuses. A fresh factory (and therefore a fresh in-memory
/// repository seeded with 草稿 / 已發布 / 已下架) is created per test.
///
/// The distinguishing feature of this table: pkid is a caller-supplied tinyint, NOT an IDENTITY.
/// </summary>
public class PublishStatusesControllerTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _client;

    public PublishStatusesControllerTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsSeededStatuses_OrderedByPkid()
    {
        var statuses = await _client.GetFromJsonAsync<List<PublishStatus>>("/api/publish-statuses");

        Assert.NotNull(statuses);
        Assert.Equal(3, statuses!.Count);
        Assert.Equal([(byte)1, (byte)2, (byte)3], statuses.Select(s => s.Pkid));
        Assert.Equal("草稿", statuses[0].Description);
        Assert.Equal(5, statuses[1].CourseCount);
        Assert.Equal(2, statuses[1].PromotionCount);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_ByKeyword_MatchesDescription()
    {
        var response = await _client.PostAsJsonAsync("/api/publish-statuses/query",
            new PublishStatusQuery { Keyword = "已發布" });
        response.EnsureSuccessStatusCode();

        var statuses = await response.Content.ReadFromJsonAsync<List<PublishStatus>>();

        var status = Assert.Single(statuses!);
        Assert.Equal((byte)2, status.Pkid);
    }

    [Fact]
    public async Task Query_ByIsDraft_ReturnsOnlyDrafts()
    {
        var response = await _client.PostAsJsonAsync("/api/publish-statuses/query",
            new PublishStatusQuery { IsDraft = true });

        var statuses = await response.Content.ReadFromJsonAsync<List<PublishStatus>>();

        var status = Assert.Single(statuses!);
        Assert.Equal("草稿", status.Description);
    }

    [Fact]
    public async Task Query_ByIsPublishedFalse_ExcludesPublished()
    {
        var response = await _client.PostAsJsonAsync("/api/publish-statuses/query",
            new PublishStatusQuery { IsPublished = false });

        var statuses = await response.Content.ReadFromJsonAsync<List<PublishStatus>>();

        Assert.Equal(2, statuses!.Count);
        Assert.DoesNotContain(statuses, s => s.IsPublished);
    }

    [Fact]
    public async Task Query_ByIsDiscontinued_ReturnsOnlyDiscontinued()
    {
        var response = await _client.PostAsJsonAsync("/api/publish-statuses/query",
            new PublishStatusQuery { IsDiscontinued = true });

        var statuses = await response.Content.ReadFromJsonAsync<List<PublishStatus>>();

        var status = Assert.Single(statuses!);
        Assert.Equal((byte)3, status.Pkid);
    }

    [Fact]
    public async Task Query_WithNoFilters_ReturnsEverything()
    {
        var response = await _client.PostAsJsonAsync("/api/publish-statuses/query", new PublishStatusQuery());

        var statuses = await response.Content.ReadFromJsonAsync<List<PublishStatus>>();

        Assert.Equal(3, statuses!.Count);
    }

    [Fact]
    public async Task Query_WithNoMatches_ReturnsEmptyList()
    {
        var response = await _client.PostAsJsonAsync("/api/publish-statuses/query",
            new PublishStatusQuery { Keyword = "沒有這個狀態" });

        var statuses = await response.Content.ReadFromJsonAsync<List<PublishStatus>>();

        Assert.Empty(statuses!);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetByPkid_ExistingStatus_ReturnsStatus()
    {
        var status = await _client.GetFromJsonAsync<PublishStatus>("/api/publish-statuses/2");

        Assert.NotNull(status);
        Assert.Equal("已發布", status!.Description);
        Assert.True(status.IsPublished);
        Assert.False(status.IsDraft);
    }

    [Fact]
    public async Task GetByPkid_MissingStatus_Returns404()
    {
        var response = await _client.GetAsync("/api/publish-statuses/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByPkid_BeyondTinyintRange_Returns404()
    {
        // 999 parses as an int and matches the {id:int} route, but no tinyint key can hold it.
        var response = await _client.GetAsync("/api/publish-statuses/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_ValidStatus_Returns201AndIsRetrievable()
    {
        var request = new PublishStatusRequest
        {
            Pkid = 10,
            Description = "審核中",
            IsDraft = true
        };

        var response = await _client.PostAsJsonAsync("/api/publish-statuses", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<PublishStatus>();
        Assert.NotNull(created);
        // The caller's pkid is honoured verbatim — it is not generated.
        Assert.Equal((byte)10, created!.Pkid);
        Assert.Equal(0, created.CourseCount);

        var fetched = await _client.GetFromJsonAsync<PublishStatus>("/api/publish-statuses/10");
        Assert.Equal("審核中", fetched!.Description);
        Assert.True(fetched.IsDraft);
    }

    [Fact]
    public async Task Create_DuplicatePkid_Returns409()
    {
        var request = new PublishStatusRequest { Pkid = 1, Description = "重複的主代碼" };

        var response = await _client.PostAsJsonAsync("/api/publish-statuses", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingDescription_Returns400()
    {
        var request = new PublishStatusRequest { Pkid = 20, Description = "" };

        var response = await _client.PostAsJsonAsync("/api/publish-statuses", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_PkidBeyondTinyintRange_Returns400()
    {
        // Sent as raw JSON: 300 does not fit in a byte, so model binding must reject it.
        var response = await _client.PostAsJsonAsync("/api/publish-statuses",
            new { pkid = 300, description = "超出範圍", isDraft = false, isPublished = false, isDiscontinued = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ExistingStatus_Returns204AndPersistsChanges()
    {
        var request = new PublishStatusRequest
        {
            Pkid = 3,
            Description = "已封存",
            IsDiscontinued = true,
            IsPublished = true
        };

        var response = await _client.PutAsJsonAsync("/api/publish-statuses", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<PublishStatus>("/api/publish-statuses/3");
        Assert.Equal("已封存", updated!.Description);
        Assert.True(updated.IsDiscontinued);
        Assert.True(updated.IsPublished);
    }

    [Fact]
    public async Task Update_AllowsAnyCombinationOfFlags()
    {
        // The schema has no CHECK constraint, so draft + published + discontinued together is legal.
        var request = new PublishStatusRequest
        {
            Pkid = 1,
            Description = "全部皆是",
            IsDraft = true,
            IsPublished = true,
            IsDiscontinued = true
        };

        var response = await _client.PutAsJsonAsync("/api/publish-statuses", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<PublishStatus>("/api/publish-statuses/1");
        Assert.True(updated!.IsDraft);
        Assert.True(updated.IsPublished);
        Assert.True(updated.IsDiscontinued);
    }

    [Fact]
    public async Task Update_MissingStatus_Returns404()
    {
        var request = new PublishStatusRequest { Pkid = 99, Description = "不存在" };

        var response = await _client.PutAsJsonAsync("/api/publish-statuses", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_UnusedStatus_Returns204()
    {
        var response = await _client.DeleteAsync("/api/publish-statuses/1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/publish-statuses/1")).StatusCode);
    }

    [Fact]
    public async Task Delete_StatusReferencedByCoursesOrPromotions_Returns409()
    {
        var response = await _client.DeleteAsync("/api/publish-statuses/2");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // Still there.
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/publish-statuses/2")).StatusCode);
    }

    [Fact]
    public async Task Delete_MissingStatus_Returns404()
    {
        var response = await _client.DeleteAsync("/api/publish-statuses/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Lookup ----------

    [Fact]
    public async Task Lookup_ReturnsAllStatusesOrderedByPkid()
    {
        var statuses = await _client.GetFromJsonAsync<List<PublishStatus>>("/api/lookups/publish-statuses");

        Assert.NotNull(statuses);
        Assert.Equal(3, statuses!.Count);
        Assert.Equal([(byte)1, (byte)2, (byte)3], statuses.Select(s => s.Pkid));
    }
}
