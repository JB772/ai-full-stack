using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// The server-side half of the「系統管理 Admin」boundary.
///
/// This is the regression suite for a real bug: a newly created account with no Admin role could open the
/// 角色 AppRole page. Three layers were wrong at once — `/` redirected everyone to /app-roles, no route guard
/// existed, and these controllers carried no role attribute — so the sidebar hiding the Admin group was
/// decoration around a page the user had already landed on.
///
/// The frontend adminGuard is a usability layer only; it runs in the user's own browser. THIS is the
/// boundary, so it gets tested per endpoint rather than per controller: a class-level attribute is easy to
/// delete, and only an explicit 403 per verb notices.
///
/// PublishStatus is deliberately asymmetric — reads stay open because course-form's FK dropdown calls
/// GET /api/publish-statuses (via PublishStatusService.getAll()); gating reads would stop non-Admins editing
/// courses at all. Only its writes are Admin-only. See PublishStatusesController's remarks.
/// </summary>
public class AdminAuthorizationTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _admin;
    private readonly HttpClient _nonAdmin;

    public AdminAuthorizationTests()
    {
        _admin = _factory.CreateAuthenticatedClient("admin", "系統管理員", "Admin");
        _nonAdmin = _factory.CreateAuthenticatedClient("guest", "訪客", "User");
    }

    public void Dispose()
    {
        _admin.Dispose();
        _nonAdmin.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---- AppRole: the reported bug. Admin-only in full. ----

    public static TheoryData<string, string> AppRoleEndpoints => new()
    {
        { "GET",    "/api/app-roles" },
        { "GET",    "/api/app-roles/1" },
        { "POST",   "/api/app-roles/query" },
        { "POST",   "/api/app-roles" },
        { "PUT",    "/api/app-roles" },
        { "DELETE", "/api/app-roles/1" }
    };

    [Theory]
    [MemberData(nameof(AppRoleEndpoints))]
    public async Task AppRole_IsForbiddenForNonAdmins(string method, string url)
        => Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_nonAdmin, method, url)).StatusCode);

    [Fact]
    public async Task AppRole_ListIsStillReachableByAdmins()
    {
        var response = await _admin.GetAsync("/api/app-roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty((await response.Content.ReadFromJsonAsync<List<AppRole>>())!);
    }

    // ---- AppUser: Admin-only in full (account CRUD was previously ungated). ----

    public static TheoryData<string, string> AppUserEndpoints => new()
    {
        { "GET",    "/api/app-users" },
        { "GET",    "/api/app-users/1" },
        { "POST",   "/api/app-users/query" },
        { "POST",   "/api/app-users" },
        { "PUT",    "/api/app-users" },
        { "DELETE", "/api/app-users/2" },
        { "GET",    "/api/app-users/1/roles" }
    };

    [Theory]
    [MemberData(nameof(AppUserEndpoints))]
    public async Task AppUser_IsForbiddenForNonAdmins(string method, string url)
        => Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_nonAdmin, method, url)).StatusCode);

    /// <summary>
    /// The concrete escalation the gate closes: AppUserRequest carries IsActive and login requires
    /// IsActive = 1, so an ungated PUT let any signed-in account lock every administrator out.
    /// </summary>
    [Fact]
    public async Task AppUser_NonAdminCannotDeactivateAnAdminAccount()
    {
        var response = await _nonAdmin.PutAsJsonAsync("/api/app-users", new AppUserRequest
        {
            Pkid = 1,
            UserId = "admin",
            UserName = "系統管理員",
            IsActive = false
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var admin = await _admin.GetFromJsonAsync<AppUser>("/api/app-users/1");
        Assert.True(admin!.IsActive); // still active — nothing was written
    }

    [Fact]
    public async Task AppUser_ListIsStillReachableByAdmins()
        => Assert.Equal(HttpStatusCode.OK, (await _admin.GetAsync("/api/app-users")).StatusCode);

    // ---- PublishStatus: writes Admin-only, reads open to every signed-in account. ----

    public static TheoryData<string, string> PublishStatusWriteEndpoints => new()
    {
        { "POST",   "/api/publish-statuses" },
        { "PUT",    "/api/publish-statuses" },
        { "DELETE", "/api/publish-statuses/1" }
    };

    [Theory]
    [MemberData(nameof(PublishStatusWriteEndpoints))]
    public async Task PublishStatus_WritesAreForbiddenForNonAdmins(string method, string url)
        => Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_nonAdmin, method, url)).StatusCode);

    /// <summary>
    /// The asymmetry, pinned. course-form.ts calls PublishStatusService.getAll() to fill its FK dropdown —
    /// if this 403s, a non-Admin cannot create or edit a course. Gating this controller at class level
    /// would break that, which is why the attribute sits on the write actions instead.
    /// </summary>
    [Fact]
    public async Task PublishStatus_ReadsStayOpenToNonAdmins_OrCourseEditingBreaks()
    {
        var list = await _nonAdmin.GetAsync("/api/publish-statuses");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.NotEmpty((await list.Content.ReadFromJsonAsync<List<PublishStatus>>())!);

        Assert.Equal(HttpStatusCode.OK, (await _nonAdmin.PostAsJsonAsync(
            "/api/publish-statuses/query", new PublishStatusQuery())).StatusCode);
    }

    // ---- The rest of the app stays open to any signed-in operator (課程管理 / 首頁 nav groups). ----

    [Theory]
    [InlineData("/api/courses")]
    [InlineData("/api/partners")]
    [InlineData("/api/course-groups")]
    [InlineData("/api/lookups/publish-statuses")]
    public async Task NonAdminEndpoints_StayReachable(string url)
        => Assert.Equal(HttpStatusCode.OK, (await _nonAdmin.GetAsync(url)).StatusCode);

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url) => method switch
    {
        "GET" => client.GetAsync(url),
        "DELETE" => client.DeleteAsync(url),
        // Bodies are deliberately empty: authorization runs before model binding, so a 403 must come back
        // regardless. A 400 here would mean the request got past the gate.
        "POST" => client.PostAsync(url, JsonContent.Create(new { })),
        "PUT" => client.PutAsync(url, JsonContent.Create(new { })),
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, "unsupported verb")
    };
}
