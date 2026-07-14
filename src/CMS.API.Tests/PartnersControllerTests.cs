using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for /api/partners. A fresh factory (and therefore a fresh in-memory repository
/// seeded with Microsoft / Cisco / Oracle) is created per test.
///
/// What distinguishes this table: pkid is a smallint IDENTITY — the database generates it — and the
/// delete guard spans five child tables, one of which (Seminar) has no FK constraint in the schema.
/// </summary>
public class PartnersControllerTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _client;

    public PartnersControllerTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static PartnerRequest ValidRequest(string name = "AWS", string appKey = "AWS") => new()
    {
        Name = name,
        AppKey = appKey,
        NameOnPartnerMenu = $"{name} 選單名稱",
        NameOnCourseDetailPage = name,
        DisplayOrder = 10
    };

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsSeededPartners_OrderedByDisplayOrderThenPkid()
    {
        var partners = await _client.GetFromJsonAsync<List<Partner>>("/api/partners");

        Assert.NotNull(partners);
        Assert.Equal(3, partners!.Count);
        Assert.Equal(["Microsoft", "Cisco", "Oracle"], partners.Select(p => p.Name));
        Assert.Equal(12, partners[0].CourseCount);
        Assert.Equal(3, partners[0].CertificationCount);
        Assert.Equal(4, partners[1].SeminarCount);
    }

    // ---------- List (query filters) ----------

    [Fact]
    public async Task Query_ByKeyword_MatchesName()
    {
        var response = await _client.PostAsJsonAsync("/api/partners/query",
            new PartnerQuery { Keyword = "Cisco" });
        response.EnsureSuccessStatusCode();

        var partners = await response.Content.ReadFromJsonAsync<List<Partner>>();

        var partner = Assert.Single(partners!);
        Assert.Equal((short)2, partner.Pkid);
    }

    [Fact]
    public async Task Query_ByKeyword_AlsoMatchesAppKey()
    {
        var response = await _client.PostAsJsonAsync("/api/partners/query",
            new PartnerQuery { Keyword = "ORCL" });

        var partners = await response.Content.ReadFromJsonAsync<List<Partner>>();

        var partner = Assert.Single(partners!);
        Assert.Equal("Oracle", partner.Name);
    }

    [Fact]
    public async Task Query_ByKeyword_AlsoMatchesTheDisplayNameColumns()
    {
        // 甲骨文 appears only in NameOnPartnerMenu / NameOnCourseDetailPage, never in Name or AppKey.
        var response = await _client.PostAsJsonAsync("/api/partners/query",
            new PartnerQuery { Keyword = "甲骨文" });

        var partners = await response.Content.ReadFromJsonAsync<List<Partner>>();

        var partner = Assert.Single(partners!);
        Assert.Equal("Oracle", partner.Name);
    }

    [Fact]
    public async Task Query_ByDisplayOrderRange_IsInclusiveAtBothEnds()
    {
        var response = await _client.PostAsJsonAsync("/api/partners/query",
            new PartnerQuery { DisplayOrderFrom = 2, DisplayOrderTo = 3 });

        var partners = await response.Content.ReadFromJsonAsync<List<Partner>>();

        Assert.Equal(2, partners!.Count);
        Assert.Equal(["Cisco", "Oracle"], partners.Select(p => p.Name));
    }

    [Fact]
    public async Task Query_ByHasImageTrue_ReturnsOnlyPartnersWithAnImage()
    {
        var response = await _client.PostAsJsonAsync("/api/partners/query",
            new PartnerQuery { HasImage = true });

        var partners = await response.Content.ReadFromJsonAsync<List<Partner>>();

        var partner = Assert.Single(partners!);
        Assert.Equal("microsoft.png", partner.ImageFilename);
    }

    [Fact]
    public async Task Query_ByHasImageFalse_ReturnsOnlyPartnersWithoutAnImage()
    {
        var response = await _client.PostAsJsonAsync("/api/partners/query",
            new PartnerQuery { HasImage = false });

        var partners = await response.Content.ReadFromJsonAsync<List<Partner>>();

        Assert.Equal(2, partners!.Count);
        Assert.All(partners, p => Assert.Null(p.ImageFilename));
    }

    [Fact]
    public async Task Query_WithNoFilters_ReturnsEverything()
    {
        var response = await _client.PostAsJsonAsync("/api/partners/query", new PartnerQuery());

        var partners = await response.Content.ReadFromJsonAsync<List<Partner>>();

        Assert.Equal(3, partners!.Count);
    }

    [Fact]
    public async Task Query_WithNoMatches_ReturnsEmptyList()
    {
        var response = await _client.PostAsJsonAsync("/api/partners/query",
            new PartnerQuery { Keyword = "沒有這個夥伴" });

        var partners = await response.Content.ReadFromJsonAsync<List<Partner>>();

        Assert.Empty(partners!);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetByPkid_ExistingPartner_ReturnsPartner()
    {
        var partner = await _client.GetFromJsonAsync<Partner>("/api/partners/1");

        Assert.NotNull(partner);
        Assert.Equal("Microsoft", partner!.Name);
        Assert.Equal("MS", partner.AppKey);
        Assert.Equal(12, partner.CourseCount);
    }

    [Fact]
    public async Task GetByPkid_MissingPartner_Returns404()
    {
        var response = await _client.GetAsync("/api/partners/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetByPkid_BeyondSmallintRange_Returns404()
    {
        // 99999 parses as an int and matches the {id:int} route, but no smallint key can hold it.
        var response = await _client.GetAsync("/api/partners/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_ValidPartner_Returns201WithAGeneratedPkid()
    {
        var response = await _client.PostAsJsonAsync("/api/partners", ValidRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<Partner>();
        Assert.NotNull(created);
        // pkid is IDENTITY-generated: it follows the seeded rows, whatever the caller sent.
        Assert.Equal((short)4, created!.Pkid);
        Assert.Equal("AWS", created.Name);
        Assert.Equal(0, created.CourseCount);

        var fetched = await _client.GetFromJsonAsync<Partner>("/api/partners/4");
        Assert.Equal("AWS", fetched!.Name);
    }

    [Fact]
    public async Task Create_IgnoresAnyPkidTheCallerSends()
    {
        var request = ValidRequest();
        request.Pkid = 99;

        var response = await _client.PostAsJsonAsync("/api/partners", request);

        var created = await response.Content.ReadFromJsonAsync<Partner>();
        // The database assigns the key — the caller does not get to pick it (unlike PublishStatus).
        Assert.Equal((short)4, created!.Pkid);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/partners/99")).StatusCode);
    }

    [Fact]
    public async Task Create_NullableImageFilename_IsAccepted()
    {
        var request = ValidRequest();
        request.ImageFilename = null;

        var response = await _client.PostAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<Partner>();
        Assert.Null(created!.ImageFilename);
    }

    [Fact]
    public async Task Create_DuplicateAppKey_IsAllowed()
    {
        // The schema puts no UNIQUE index on AppKey, so the API must not invent a 409 here.
        var request = ValidRequest(name: "Microsoft Azure", appKey: "MS");

        var response = await _client.PostAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("")]                 // Name is required
    [InlineData("超過五十個字元的名稱超過五十個字元的名稱超過五十個字元的名稱超過五十個字元的名稱超過五十個字元的名稱超過")] // > 50
    public async Task Create_InvalidName_Returns400(string name)
    {
        var request = ValidRequest();
        request.Name = name;

        var response = await _client.PostAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingAppKey_Returns400()
    {
        var request = ValidRequest();
        request.AppKey = "";

        var response = await _client.PostAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_AppKeyLongerThanTenChars_Returns400()
    {
        var request = ValidRequest();
        request.AppKey = "TOOLONGAPPKEY";

        var response = await _client.PostAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingNameOnPartnerMenu_Returns400()
    {
        var request = ValidRequest();
        request.NameOnPartnerMenu = "";

        var response = await _client.PostAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingNameOnCourseDetailPage_Returns400()
    {
        var request = ValidRequest();
        request.NameOnCourseDetailPage = "";

        var response = await _client.PostAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_NegativeDisplayOrder_Returns400()
    {
        var request = ValidRequest();
        request.DisplayOrder = -1;

        var response = await _client.PostAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ExistingPartner_Returns204AndPersistsChanges()
    {
        var request = ValidRequest(name: "Oracle 更新", appKey: "ORCL2");
        request.Pkid = 3;
        request.DisplayOrder = 7;
        request.ImageFilename = "oracle.png";

        var response = await _client.PutAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<Partner>("/api/partners/3");
        Assert.Equal("Oracle 更新", updated!.Name);
        // AppKey is freely editable — nothing foreign-keys to it (contrast AppRole.RoleId).
        Assert.Equal("ORCL2", updated.AppKey);
        Assert.Equal(7, updated.DisplayOrder);
        Assert.Equal("oracle.png", updated.ImageFilename);
    }

    [Fact]
    public async Task Update_CanClearTheImageFilename()
    {
        var request = ValidRequest(name: "Microsoft", appKey: "MS");
        request.Pkid = 1;
        request.ImageFilename = null;

        var response = await _client.PutAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<Partner>("/api/partners/1");
        Assert.Null(updated!.ImageFilename);
    }

    [Fact]
    public async Task Update_WithoutAPkid_Returns400()
    {
        // pkid is an IDENTITY seeded at 1, so 0 means "absent" — a 400, not a 404.
        var request = ValidRequest();
        request.Pkid = 0;

        var response = await _client.PutAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_MissingPartner_Returns404()
    {
        var request = ValidRequest();
        request.Pkid = 99;

        var response = await _client.PutAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_MissingRequiredField_Returns400()
    {
        var request = ValidRequest();
        request.Pkid = 1;
        request.Name = "";

        var response = await _client.PutAsJsonAsync("/api/partners", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_UnreferencedPartner_Returns204()
    {
        var response = await _client.DeleteAsync("/api/partners/3");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/partners/3")).StatusCode);
    }

    [Fact]
    public async Task Delete_PartnerWithCoursesAndCertifications_Returns409AndNamesBoth()
    {
        var response = await _client.DeleteAsync("/api/partners/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("12 筆課程", body);
        Assert.Contains("3 筆認證", body);
        // Only the child tables that actually have rows are named.
        Assert.DoesNotContain("促銷活動", body);

        // Still there.
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/partners/1")).StatusCode);
    }

    [Fact]
    public async Task Delete_PartnerReferencedOnlyBySeminars_Returns409()
    {
        // Seminar.Partner_pkid has no FK constraint in the schema, so SQL Server would happily orphan
        // these rows. The guard blocks the delete anyway.
        var response = await _client.DeleteAsync("/api/partners/2");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("4 筆研討會", body);

        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/partners/2")).StatusCode);
    }

    [Fact]
    public async Task Delete_MissingPartner_Returns404()
    {
        var response = await _client.DeleteAsync("/api/partners/99");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_BeyondSmallintRange_Returns404()
    {
        var response = await _client.DeleteAsync("/api/partners/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ANewlyCreatedPartner_Succeeds()
    {
        // A partner nothing references yet has all five counts at zero.
        var created = await (await _client.PostAsJsonAsync("/api/partners", ValidRequest()))
            .Content.ReadFromJsonAsync<Partner>();

        var response = await _client.DeleteAsync($"/api/partners/{created!.Pkid}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ---------- Lookup ----------

    [Fact]
    public async Task Lookup_ReturnsAllPartnersOrderedByDisplayOrder()
    {
        var partners = await _client.GetFromJsonAsync<List<Partner>>("/api/lookups/partners");

        Assert.NotNull(partners);
        Assert.Equal(3, partners!.Count);
        Assert.Equal(["Microsoft", "Cisco", "Oracle"], partners.Select(p => p.Name));
    }
}
