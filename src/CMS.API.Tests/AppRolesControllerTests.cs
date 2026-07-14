using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for /api/app-roles. A fresh factory (and therefore a fresh in-memory
/// repository seeded with Admin + User) is created per test — xUnit builds a new class
/// instance for every test method, so writes in one test cannot leak into another.
/// </summary>
public class AppRolesControllerTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _client;

    public AppRolesControllerTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsSeededRoles_OrderedByRoleId()
    {
        var roles = await _client.GetFromJsonAsync<List<AppRole>>("/api/app-roles");

        Assert.NotNull(roles);
        Assert.Equal(2, roles!.Count);
        Assert.Equal(["Admin", "User"], roles.Select(r => r.RoleId));
        Assert.Equal(3, roles[0].UserCount);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_ByKeyword_MatchesRoleName()
    {
        var response = await _client.PostAsJsonAsync("/api/app-roles/query", new AppRoleQuery { Keyword = "Administrator" });
        response.EnsureSuccessStatusCode();

        var roles = await response.Content.ReadFromJsonAsync<List<AppRole>>();

        Assert.NotNull(roles);
        var role = Assert.Single(roles!);
        Assert.Equal("Admin", role.RoleId);
    }

    [Fact]
    public async Task Query_ByKeyword_MatchesDescription()
    {
        var response = await _client.PostAsJsonAsync("/api/app-roles/query", new AppRoleQuery { Keyword = "一般" });

        var roles = await response.Content.ReadFromJsonAsync<List<AppRole>>();

        var role = Assert.Single(roles!);
        Assert.Equal("User", role.RoleId);
    }

    [Fact]
    public async Task Query_ByPermissionLevelRange_FiltersInclusively()
    {
        var response = await _client.PostAsJsonAsync("/api/app-roles/query",
            new AppRoleQuery { PermissionLevelFrom = 50, PermissionLevelTo = 100 });

        var roles = await response.Content.ReadFromJsonAsync<List<AppRole>>();

        var role = Assert.Single(roles!);
        Assert.Equal(100, role.PermissionLevel);
    }

    [Fact]
    public async Task Query_WithNoFilters_ReturnsEverything()
    {
        var response = await _client.PostAsJsonAsync("/api/app-roles/query", new AppRoleQuery());

        var roles = await response.Content.ReadFromJsonAsync<List<AppRole>>();

        Assert.Equal(2, roles!.Count);
    }

    [Fact]
    public async Task Query_WithNoMatches_ReturnsEmptyList()
    {
        var response = await _client.PostAsJsonAsync("/api/app-roles/query", new AppRoleQuery { Keyword = "沒有這個角色" });

        var roles = await response.Content.ReadFromJsonAsync<List<AppRole>>();

        Assert.Empty(roles!);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetByPkid_ExistingRole_ReturnsRole()
    {
        var role = await _client.GetFromJsonAsync<AppRole>("/api/app-roles/1");

        Assert.NotNull(role);
        Assert.Equal("Admin", role!.RoleId);
        Assert.Equal("Administrator", role.RoleName);
        Assert.Equal(1, role.PermissionLevel);
        Assert.Equal("系統管理員", role.Description);
    }

    [Fact]
    public async Task GetByPkid_MissingRole_Returns404()
    {
        var response = await _client.GetAsync("/api/app-roles/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_ValidRole_Returns201AndIsRetrievable()
    {
        var request = new AppRoleRequest
        {
            RoleId = "Editor",
            RoleName = "Content Editor",
            PermissionLevel = 50,
            Description = "內容編輯者"
        };

        var response = await _client.PostAsJsonAsync("/api/app-roles", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<AppRole>();
        Assert.NotNull(created);
        Assert.True(created!.Pkid > 0);
        Assert.Equal("Editor", created.RoleId);
        Assert.Equal(0, created.UserCount);

        var fetched = await _client.GetFromJsonAsync<AppRole>($"/api/app-roles/{created.Pkid}");
        Assert.Equal("Content Editor", fetched!.RoleName);
        Assert.Equal(50, fetched.PermissionLevel);
    }

    [Fact]
    public async Task Create_DuplicateRoleId_Returns409()
    {
        var request = new AppRoleRequest { RoleId = "Admin", RoleName = "Duplicate", PermissionLevel = 1 };

        var response = await _client.PostAsJsonAsync("/api/app-roles", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingRequiredFields_Returns400()
    {
        var request = new AppRoleRequest { RoleId = "", RoleName = "", PermissionLevel = 10 };

        var response = await _client.PostAsJsonAsync("/api/app-roles", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ExistingRole_Returns204AndPersistsChanges()
    {
        var request = new AppRoleRequest
        {
            Pkid = 2,
            RoleId = "User",
            RoleName = "一般使用者",
            PermissionLevel = 200,
            Description = "更新後的描述"
        };

        var response = await _client.PutAsJsonAsync("/api/app-roles", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<AppRole>("/api/app-roles/2");
        Assert.Equal("一般使用者", updated!.RoleName);
        Assert.Equal(200, updated.PermissionLevel);
        Assert.Equal("更新後的描述", updated.Description);
    }

    [Fact]
    public async Task Update_DoesNotChangeRoleId()
    {
        var request = new AppRoleRequest
        {
            Pkid = 2,
            RoleId = "RenamedUser",
            RoleName = "User",
            PermissionLevel = 100
        };

        await _client.PutAsJsonAsync("/api/app-roles", request);

        var updated = await _client.GetFromJsonAsync<AppRole>("/api/app-roles/2");
        Assert.Equal("User", updated!.RoleId);
    }

    [Fact]
    public async Task Update_MissingRole_Returns404()
    {
        var request = new AppRoleRequest { Pkid = 999, RoleId = "Ghost", RoleName = "Ghost", PermissionLevel = 1 };

        var response = await _client.PutAsJsonAsync("/api/app-roles", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutPkid_Returns400()
    {
        var request = new AppRoleRequest { Pkid = 0, RoleId = "Admin", RoleName = "Administrator", PermissionLevel = 1 };

        var response = await _client.PutAsJsonAsync("/api/app-roles", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_RoleWithoutUsers_Returns204()
    {
        var response = await _client.DeleteAsync("/api/app-roles/2");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/app-roles/2")).StatusCode);
    }

    [Fact]
    public async Task Delete_RoleWithUsers_Returns409()
    {
        var response = await _client.DeleteAsync("/api/app-roles/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingRole_Returns404()
    {
        var response = await _client.DeleteAsync("/api/app-roles/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
