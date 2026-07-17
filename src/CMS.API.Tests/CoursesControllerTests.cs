using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for /api/courses. A fresh factory (and therefore a fresh in-memory repository seeded
/// with Azure 基礎 / CCNA / Java SE) is created per test.
///
/// What distinguishes this table: it is the first with outbound FKs, so responses carry Partner /
/// CourseGroup / PublishStatus nav objects (CourseGroup is null when the FK is null); pkid is an int
/// IDENTITY; and the delete guard spans six child tables, two of which (CourseInCertification,
/// CourseJobCategories) are ON DELETE CASCADE.
/// </summary>
public class CoursesControllerTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _client;

    public CoursesControllerTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static CourseRequest ValidRequest(string title = "新課程", string courseId = "NEW-101") => new()
    {
        Title = title,
        CourseId = courseId,
        ProdCourseId = $"PROD-{courseId}",
        FriendlyUrl = courseId.ToLowerInvariant(),
        DisplayOrder = 10,
        PartnerPkid = 1,
        CourseGroupPkid = 1,
        PublishStatusPkid = 2,
        ScheduleOn = new DateOnly(2026, 1, 1),
        ScheduleOff = new DateOnly(2036, 1, 1),
        Hour = 8,
        ListPrice = 5000m,
        LearningCredit = 2.5m,
        CanRepeat = false
    };

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsSeededCourses_OrderedByDisplayOrderThenPkid()
    {
        var courses = await _client.GetFromJsonAsync<List<Course>>("/api/courses");

        Assert.NotNull(courses);
        Assert.Equal(3, courses!.Count);
        Assert.Equal(["Azure 基礎", "CCNA 認證課程", "Java SE 程式設計"], courses.Select(c => c.Title));
    }

    [Fact]
    public async Task GetAll_PopulatesFkNavObjects()
    {
        var courses = await _client.GetFromJsonAsync<List<Course>>("/api/courses");

        var azure = courses!.Single(c => c.Pkid == 1);
        Assert.Equal("Microsoft", azure.Partner!.Name);
        Assert.Equal("雲端服務", azure.CourseGroup!.Description);
        Assert.Equal("已上架", azure.PublishStatus!.Description);
    }

    [Fact]
    public async Task GetAll_NullCourseGroup_ComesBackAsNull()
    {
        // CCNA has no CourseGroup — the LEFT JOIN yields a null nav object, not an empty one.
        var courses = await _client.GetFromJsonAsync<List<Course>>("/api/courses");

        var ccna = courses!.Single(c => c.Pkid == 2);
        Assert.Null(ccna.CourseGroupPkid);
        Assert.Null(ccna.CourseGroup);
    }

    // ---------- List (query filters) ----------

    [Fact]
    public async Task Query_ByKeyword_MatchesTitle()
    {
        var response = await _client.PostAsJsonAsync("/api/courses/query", new CourseQuery { Keyword = "Azure" });

        var courses = await response.Content.ReadFromJsonAsync<List<Course>>();

        var course = Assert.Single(courses!);
        Assert.Equal(1, course.Pkid);
    }

    [Fact]
    public async Task Query_ByKeyword_AlsoMatchesCourseId()
    {
        var response = await _client.PostAsJsonAsync("/api/courses/query", new CourseQuery { Keyword = "CCNA" });

        var courses = await response.Content.ReadFromJsonAsync<List<Course>>();

        var course = Assert.Single(courses!);
        Assert.Equal("CCNA 認證課程", course.Title);
    }

    [Fact]
    public async Task Query_ByPartner_ReturnsOnlyThatPartnersCourses()
    {
        var response = await _client.PostAsJsonAsync("/api/courses/query", new CourseQuery { PartnerPkid = 3 });

        var courses = await response.Content.ReadFromJsonAsync<List<Course>>();

        var course = Assert.Single(courses!);
        Assert.Equal("Java SE 程式設計", course.Title);
    }

    [Fact]
    public async Task Query_ByCourseGroup_ReturnsMatches()
    {
        var response = await _client.PostAsJsonAsync("/api/courses/query", new CourseQuery { CourseGroupPkid = 1 });

        var courses = await response.Content.ReadFromJsonAsync<List<Course>>();

        var course = Assert.Single(courses!);
        Assert.Equal(1, course.Pkid);
    }

    [Fact]
    public async Task Query_ByPublishStatus_ReturnsMatches()
    {
        var response = await _client.PostAsJsonAsync("/api/courses/query", new CourseQuery { PublishStatusPkid = 2 });

        var courses = await response.Content.ReadFromJsonAsync<List<Course>>();

        Assert.Equal(2, courses!.Count);
        Assert.Equal(["Azure 基礎", "Java SE 程式設計"], courses.Select(c => c.Title));
    }

    [Fact]
    public async Task Query_ByScheduleOnRange_IsInclusive()
    {
        var response = await _client.PostAsJsonAsync("/api/courses/query", new CourseQuery
        {
            ScheduleOnFrom = new DateOnly(2026, 2, 1),
            ScheduleOnTo = new DateOnly(2026, 6, 1)
        });

        var courses = await response.Content.ReadFromJsonAsync<List<Course>>();

        Assert.Equal(2, courses!.Count);
        Assert.Equal(["CCNA 認證課程", "Java SE 程式設計"], courses.Select(c => c.Title));
    }

    [Fact]
    public async Task Query_ByCanRepeatTrue_ReturnsOnlyRepeatableCourses()
    {
        var response = await _client.PostAsJsonAsync("/api/courses/query", new CourseQuery { CanRepeat = true });

        var courses = await response.Content.ReadFromJsonAsync<List<Course>>();

        Assert.Equal(2, courses!.Count);
        Assert.All(courses, c => Assert.True(c.CanRepeat));
    }

    [Fact]
    public async Task Query_WithNoFilters_ReturnsEverything()
    {
        var response = await _client.PostAsJsonAsync("/api/courses/query", new CourseQuery());

        var courses = await response.Content.ReadFromJsonAsync<List<Course>>();

        Assert.Equal(3, courses!.Count);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetByPkid_ExistingCourse_ReturnsCourse()
    {
        var course = await _client.GetFromJsonAsync<Course>("/api/courses/1");

        Assert.NotNull(course);
        Assert.Equal("Azure 基礎", course!.Title);
        Assert.Equal("AZ-900", course.CourseId);
        Assert.Equal(2, course.CertificationCount);
    }

    [Fact]
    public async Task GetByPkid_MissingCourse_Returns404()
    {
        var response = await _client.GetAsync("/api/courses/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_ValidCourse_Returns201WithAGeneratedPkid()
    {
        var response = await _client.PostAsJsonAsync("/api/courses", ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<Course>();
        Assert.NotNull(created);
        // pkid is IDENTITY-generated: it follows the seeded rows, whatever the caller sent.
        Assert.Equal(4, created!.Pkid);
        Assert.Equal("新課程", created.Title);
        Assert.Equal("Microsoft", created.Partner!.Name);
        Assert.Equal(0, created.CertificationCount);

        var fetched = await _client.GetFromJsonAsync<Course>("/api/courses/4");
        Assert.Equal("新課程", fetched!.Title);
    }

    [Fact]
    public async Task Create_IgnoresAnyPkidTheCallerSends()
    {
        var request = ValidRequest();
        request.Pkid = 99;

        var response = await _client.PostAsJsonAsync("/api/courses", request);

        var created = await response.Content.ReadFromJsonAsync<Course>();
        Assert.Equal(4, created!.Pkid);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/courses/99")).StatusCode);
    }

    [Fact]
    public async Task Create_NullCourseGroup_IsAccepted()
    {
        var request = ValidRequest();
        request.CourseGroupPkid = null;

        var response = await _client.PostAsJsonAsync("/api/courses", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<Course>();
        Assert.Null(created!.CourseGroupPkid);
        Assert.Null(created.CourseGroup);
    }

    [Fact]
    public async Task Create_MissingTitle_Returns400()
    {
        var request = ValidRequest();
        request.Title = "";

        var response = await _client.PostAsJsonAsync("/api/courses", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_TitleLongerThan200Chars_Returns400()
    {
        var request = ValidRequest();
        request.Title = new string('課', 201);

        var response = await _client.PostAsJsonAsync("/api/courses", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingCourseId_Returns400()
    {
        var request = ValidRequest();
        request.CourseId = "";

        var response = await _client.PostAsJsonAsync("/api/courses", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_NegativeDisplayOrder_Returns400()
    {
        var request = ValidRequest();
        request.DisplayOrder = -1;

        var response = await _client.PostAsJsonAsync("/api/courses", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ExistingCourse_Returns204AndPersistsChanges()
    {
        var request = ValidRequest(title: "Azure 進階", courseId: "AZ-900");
        request.Pkid = 1;
        request.Hour = 24;
        request.CanRepeat = false;

        var response = await _client.PutAsJsonAsync("/api/courses", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<Course>("/api/courses/1");
        Assert.Equal("Azure 進階", updated!.Title);
        Assert.Equal((short)24, updated.Hour);
        Assert.False(updated.CanRepeat);
    }

    [Fact]
    public async Task Update_CanReassignTheCourseGroupToNull()
    {
        var request = ValidRequest(title: "Azure 基礎", courseId: "AZ-900");
        request.Pkid = 1;
        request.CourseGroupPkid = null;

        var response = await _client.PutAsJsonAsync("/api/courses", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<Course>("/api/courses/1");
        Assert.Null(updated!.CourseGroupPkid);
        Assert.Null(updated.CourseGroup);
    }

    [Fact]
    public async Task Update_WithoutAPkid_Returns400()
    {
        // pkid is an IDENTITY seeded at 1, so 0 means "absent" — a 400, not a 404.
        var request = ValidRequest();
        request.Pkid = 0;

        var response = await _client.PutAsJsonAsync("/api/courses", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_MissingCourse_Returns404()
    {
        var request = ValidRequest();
        request.Pkid = 99;

        var response = await _client.PutAsJsonAsync("/api/courses", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_MissingRequiredField_Returns400()
    {
        var request = ValidRequest();
        request.Pkid = 1;
        request.Title = "";

        var response = await _client.PutAsJsonAsync("/api/courses", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_UnreferencedCourse_Returns204()
    {
        var response = await _client.DeleteAsync("/api/courses/2");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/courses/2")).StatusCode);
    }

    [Fact]
    public async Task Delete_CourseWithFaqAndCertifications_Returns409AndNamesBoth()
    {
        var response = await _client.DeleteAsync("/api/courses/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("1 筆問答", body);
        Assert.Contains("2 筆認證關聯", body);
        // Only the child tables that actually have rows are named.
        Assert.DoesNotContain("熱門課程", body);

        // Still there.
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/courses/1")).StatusCode);
    }

    [Fact]
    public async Task Delete_CourseReferencedOnlyByHotCourse_Returns409()
    {
        var response = await _client.DeleteAsync("/api/courses/3");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("3 筆熱門課程", body);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/courses/3")).StatusCode);
    }

    [Fact]
    public async Task Delete_MissingCourse_Returns404()
    {
        var response = await _client.DeleteAsync("/api/courses/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The TOCTOU case. The controller's pre-check runs on a different connection than the DELETE, so a
    /// CourseInCertification row can land in the window between them — and that FK is ON DELETE CASCADE,
    /// so SQL Server destroys it silently rather than rejecting the delete. Only an in-transaction
    /// re-check stops that; every other delete test here is answered by the controller pre-check and
    /// returns before the repository guard runs.
    ///
    /// SCOPE: `CmsApiFactory` swaps in `InMemoryCourseRepository`, so this covers the FAKE's guard plus
    /// the controller's `DeleteResult.Blocked` → 409 mapping — not `CourseRepository`'s real Dapper
    /// guard, which no endpoint test can reach. See the equivalent test in CourseGroupsControllerTests
    /// for the negative-control evidence behind that claim.
    /// </summary>
    [Fact]
    public async Task Delete_WhenACertificationAppearsAfterThePreCheck_Returns409AndKeepsTheCourse()
    {
        // Course 2 has no children, so the controller's pre-check passes and we reach the repository.
        var fake = (InMemoryCourseRepository)_factory.Services.GetRequiredService<ICourseRepository>();
        fake.OnBeforeDeleteGuard = course => course.CertificationCount = 1; // lands in the race window

        var response = await _client.DeleteAsync("/api/courses/2");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // The course — and the junction row that would have been cascaded away — must survive.
        fake.OnBeforeDeleteGuard = null;
        var survivor = await _client.GetFromJsonAsync<Course>("/api/courses/2");
        Assert.Equal("CCNA 認證課程", survivor!.Title);
    }

    [Fact]
    public async Task Delete_ANewlyCreatedCourse_Succeeds()
    {
        // A course nothing references yet has all six counts at zero.
        var created = await (await _client.PostAsJsonAsync("/api/courses", ValidRequest()))
            .Content.ReadFromJsonAsync<Course>();

        var response = await _client.DeleteAsync($"/api/courses/{created!.Pkid}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}
