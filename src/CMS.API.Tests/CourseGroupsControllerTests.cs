using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for /api/course-groups. A fresh factory (and therefore a fresh in-memory repository
/// seeded with 雲端 / 資料庫 / 資訊安全) is created per test.
///
/// The distinguishing feature of this table: FK_Course_CourseGroup is ON DELETE CASCADE, so the database
/// will NOT stop a delete that has courses under it — it will destroy them. The 409 guard is the only
/// protection, which makes the delete tests the most important ones in this file.
/// </summary>
public class CourseGroupsControllerTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _client;

    public CourseGroupsControllerTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsSeededGroups_OrderedByDescription()
    {
        var groups = await _client.GetFromJsonAsync<List<CourseGroup>>("/api/course-groups");

        Assert.NotNull(groups);
        Assert.Equal(3, groups!.Count);

        // The sort key is Description, not pkid. Seeded pkid order would be [雲端, 資料庫, 資訊安全];
        // any Description sort moves 雲端 to the end, which is what this pins.
        //
        // The exact sequence below is the fake's StringComparer.Ordinal (code-point) order. Real SQL Server
        // orders nvarchar by the database collation, so the precise Chinese ordering in production may differ
        // — that is a collation concern no in-memory comparer can reproduce, and not what this test asserts.
        Assert.Equal(["資料庫", "資訊安全", "雲端"], groups.Select(g => g.Description));
        Assert.Equal(12, groups.Single(g => g.Description == "資料庫").CourseCount);
        Assert.Equal(3, groups.Single(g => g.Description == "資料庫").PartnerCourseGroupCount);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_ByKeyword_MatchesDescription()
    {
        var response = await _client.PostAsJsonAsync("/api/course-groups/query",
            new CourseGroupQuery { Keyword = "資料庫" });
        response.EnsureSuccessStatusCode();

        var groups = await response.Content.ReadFromJsonAsync<List<CourseGroup>>();

        var group = Assert.Single(groups!);
        Assert.Equal((short)2, group.Pkid);
    }

    [Fact]
    public async Task Query_ByPartialKeyword_MatchesSubstring()
    {
        var response = await _client.PostAsJsonAsync("/api/course-groups/query",
            new CourseGroupQuery { Keyword = "資" });

        var groups = await response.Content.ReadFromJsonAsync<List<CourseGroup>>();

        Assert.Equal(2, groups!.Count);
        Assert.All(groups, g => Assert.Contains("資", g.Description));
    }

    [Fact]
    public async Task Query_WithNoFilters_ReturnsEverything()
    {
        var response = await _client.PostAsJsonAsync("/api/course-groups/query", new CourseGroupQuery());

        var groups = await response.Content.ReadFromJsonAsync<List<CourseGroup>>();

        Assert.Equal(3, groups!.Count);
    }

    [Fact]
    public async Task Query_WithNoMatches_ReturnsEmptyList()
    {
        var response = await _client.PostAsJsonAsync("/api/course-groups/query",
            new CourseGroupQuery { Keyword = "沒有這個群組" });

        var groups = await response.Content.ReadFromJsonAsync<List<CourseGroup>>();

        Assert.Empty(groups!);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetByPkid_ExistingGroup_ReturnsGroupWithCounts()
    {
        var group = await _client.GetFromJsonAsync<CourseGroup>("/api/course-groups/2");

        Assert.NotNull(group);
        Assert.Equal("資料庫", group!.Description);
        Assert.Equal(12, group.CourseCount);
        Assert.Equal(3, group.PartnerCourseGroupCount);
    }

    [Fact]
    public async Task GetByPkid_MissingGroup_Returns404()
    {
        var response = await _client.GetAsync("/api/course-groups/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByPkid_BeyondSmallintRange_Returns404()
    {
        // 99999 parses as an int and matches the {id:int} route, but no smallint key can hold it.
        var response = await _client.GetAsync("/api/course-groups/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_ValidGroup_Returns201WithGeneratedPkid()
    {
        var request = new CourseGroupRequest { Description = "人工智慧" };

        var response = await _client.PostAsJsonAsync("/api/course-groups", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CourseGroup>();
        Assert.NotNull(created);
        // pkid is IDENTITY-generated — the caller never supplies it.
        Assert.Equal((short)4, created!.Pkid);
        Assert.Equal("人工智慧", created.Description);
        Assert.Equal(0, created.CourseCount);

        var fetched = await _client.GetFromJsonAsync<CourseGroup>("/api/course-groups/4");
        Assert.Equal("人工智慧", fetched!.Description);
    }

    [Fact]
    public async Task Create_IgnoresAnyPkidTheCallerSends()
    {
        // pkid is IDENTITY — a caller-supplied value must not be honoured.
        var request = new CourseGroupRequest { Pkid = 99, Description = "人工智慧" };

        var response = await _client.PostAsJsonAsync("/api/course-groups", request);

        var created = await response.Content.ReadFromJsonAsync<CourseGroup>();
        Assert.Equal((short)4, created!.Pkid);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/course-groups/99")).StatusCode);
    }

    [Fact]
    public async Task Create_MissingDescription_Returns400()
    {
        var request = new CourseGroupRequest { Description = "" };

        var response = await _client.PostAsJsonAsync("/api/course-groups", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DescriptionBeyond100Chars_Returns400()
    {
        var request = new CourseGroupRequest { Description = new string('雲', 101) };

        var response = await _client.PostAsJsonAsync("/api/course-groups", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_DuplicateDescription_IsAllowed()
    {
        // The schema puts no UNIQUE index on Description, so the API must not invent one.
        var response = await _client.PostAsJsonAsync("/api/course-groups",
            new CourseGroupRequest { Description = "雲端" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ExistingGroup_Returns204AndPersistsChanges()
    {
        var request = new CourseGroupRequest { Pkid = 1, Description = "雲端運算" };

        var response = await _client.PutAsJsonAsync("/api/course-groups", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<CourseGroup>("/api/course-groups/1");
        Assert.Equal("雲端運算", updated!.Description);
    }

    [Fact]
    public async Task Update_MissingPkid_Returns400()
    {
        // pkid is an IDENTITY seeded at 1, so 0 means "absent" — a 400, not a 404.
        var request = new CourseGroupRequest { Pkid = 0, Description = "沒有主代碼" };

        var response = await _client.PutAsJsonAsync("/api/course-groups", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_MissingGroup_Returns404()
    {
        var request = new CourseGroupRequest { Pkid = 99, Description = "不存在" };

        var response = await _client.PutAsJsonAsync("/api/course-groups", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_UnusedGroup_Returns204()
    {
        var response = await _client.DeleteAsync("/api/course-groups/1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/course-groups/1")).StatusCode);
    }

    /// <summary>
    /// The one that matters. FK_Course_CourseGroup is ON DELETE CASCADE: without this guard SQL Server
    /// would return success having deleted all 12 courses. The group must survive the attempt.
    /// </summary>
    [Fact]
    public async Task Delete_GroupWithCourses_Returns409AndLeavesTheGroupIntact()
    {
        var response = await _client.DeleteAsync("/api/course-groups/2");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("12 筆課程", body);

        var survivor = await _client.GetFromJsonAsync<CourseGroup>("/api/course-groups/2");
        Assert.Equal("資料庫", survivor!.Description);
    }

    [Fact]
    public async Task Delete_GroupWithOnlyPartnerCourseGroups_Returns409()
    {
        // No courses, so no cascade — but FK_PartnerCourseGroup_CourseGroup would still be violated.
        var response = await _client.DeleteAsync("/api/course-groups/3");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("2 筆夥伴課程群組", body);
        // The message names only the child tables that actually have rows.
        Assert.DoesNotContain("筆課程，", body);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/course-groups/3")).StatusCode);
    }

    [Fact]
    public async Task Delete_MissingGroup_Returns404()
    {
        var response = await _client.DeleteAsync("/api/course-groups/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Lookup ----------

    [Fact]
    public async Task Lookup_ReturnsAllGroupsOrderedByDescription()
    {
        var groups = await _client.GetFromJsonAsync<List<CourseGroup>>("/api/lookups/course-groups");

        Assert.NotNull(groups);
        Assert.Equal(3, groups!.Count);
        // Same ordering as the list endpoint — the lookup reuses GetAllAsync. See the note there.
        Assert.Equal(["資料庫", "資訊安全", "雲端"], groups.Select(g => g.Description));
    }
}
