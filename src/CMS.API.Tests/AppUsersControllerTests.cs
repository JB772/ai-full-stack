using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for /api/app-users. A fresh factory (and in-memory repository seeded with
/// admin + guest) is created per test. PasswordHash is backend-only, so it never appears in any
/// request or response here — the create/update contract carries no password.
/// </summary>
public class AppUsersControllerTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _client;

    public AppUsersControllerTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---------- List ----------

    [Fact]
    public async Task GetAll_ReturnsSeededUsers_OrderedByUserId()
    {
        var users = await _client.GetFromJsonAsync<List<AppUser>>("/api/app-users");

        Assert.NotNull(users);
        Assert.Equal(2, users!.Count);
        Assert.Equal(["admin", "guest"], users.Select(u => u.UserId));
        Assert.Equal(2, users[0].RoleCount);
    }

    // ---------- List (query filter) ----------

    [Fact]
    public async Task Query_ByKeyword_MatchesUserName()
    {
        var response = await _client.PostAsJsonAsync("/api/app-users/query", new AppUserQuery { Keyword = "訪客" });
        response.EnsureSuccessStatusCode();

        var users = await response.Content.ReadFromJsonAsync<List<AppUser>>();

        var user = Assert.Single(users!);
        Assert.Equal("guest", user.UserId);
    }

    [Fact]
    public async Task Query_ByIsActiveFalse_ReturnsOnlyInactive()
    {
        var response = await _client.PostAsJsonAsync("/api/app-users/query", new AppUserQuery { IsActive = false });

        var users = await response.Content.ReadFromJsonAsync<List<AppUser>>();

        var user = Assert.Single(users!);
        Assert.Equal("guest", user.UserId);
        Assert.False(user.IsActive);
    }

    [Fact]
    public async Task Query_WithNoFilters_ReturnsEverything()
    {
        var response = await _client.PostAsJsonAsync("/api/app-users/query", new AppUserQuery());

        var users = await response.Content.ReadFromJsonAsync<List<AppUser>>();

        Assert.Equal(2, users!.Count);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetByPkid_ExistingUser_ReturnsUser()
    {
        var user = await _client.GetFromJsonAsync<AppUser>("/api/app-users/1");

        Assert.NotNull(user);
        Assert.Equal("admin", user!.UserId);
        Assert.Equal("系統管理員", user.UserName);
        Assert.True(user.IsActive);
    }

    [Fact]
    public async Task GetByPkid_MissingUser_Returns404()
    {
        var response = await _client.GetAsync("/api/app-users/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_ValidUser_Returns201AndIsRetrievable()
    {
        var request = new AppUserRequest { UserId = "editor", UserName = "編輯者", IsActive = true };

        var response = await _client.PostAsJsonAsync("/api/app-users", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<AppUser>();
        Assert.NotNull(created);
        Assert.True(created!.Pkid > 0);
        Assert.Equal("editor", created.UserId);
        Assert.Equal(0, created.RoleCount);
        // New accounts hold the default password — it has not been *updated* yet.
        Assert.Null(created.PasswordUpdatedTime);

        var fetched = await _client.GetFromJsonAsync<AppUser>($"/api/app-users/{created.Pkid}");
        Assert.Equal("編輯者", fetched!.UserName);
    }

    [Fact]
    public async Task Create_DuplicateUserId_Returns409()
    {
        var request = new AppUserRequest { UserId = "admin", UserName = "Duplicate", IsActive = true };

        var response = await _client.PostAsJsonAsync("/api/app-users", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Create_MissingRequiredFields_Returns400()
    {
        var request = new AppUserRequest { UserId = "", UserName = "", IsActive = true };

        var response = await _client.PostAsJsonAsync("/api/app-users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ExistingUser_Returns204AndPersistsChanges()
    {
        var request = new AppUserRequest { Pkid = 2, UserId = "guest", UserName = "訪客帳號", IsActive = true };

        var response = await _client.PutAsJsonAsync("/api/app-users", request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var updated = await _client.GetFromJsonAsync<AppUser>("/api/app-users/2");
        Assert.Equal("訪客帳號", updated!.UserName);
        Assert.True(updated.IsActive);
    }

    [Fact]
    public async Task Update_DoesNotChangeUserId()
    {
        var request = new AppUserRequest { Pkid = 2, UserId = "renamed", UserName = "訪客", IsActive = false };

        await _client.PutAsJsonAsync("/api/app-users", request);

        var updated = await _client.GetFromJsonAsync<AppUser>("/api/app-users/2");
        Assert.Equal("guest", updated!.UserId);
    }

    [Fact]
    public async Task Update_MissingUser_Returns404()
    {
        var request = new AppUserRequest { Pkid = 999, UserId = "ghost", UserName = "Ghost", IsActive = true };

        var response = await _client.PutAsJsonAsync("/api/app-users", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithoutPkid_Returns400()
    {
        var request = new AppUserRequest { Pkid = 0, UserId = "admin", UserName = "系統管理員", IsActive = true };

        var response = await _client.PutAsJsonAsync("/api/app-users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_UserWithoutRoles_Returns204()
    {
        var response = await _client.DeleteAsync("/api/app-users/2");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/app-users/2")).StatusCode);
    }

    [Fact]
    public async Task Delete_UserWithRoles_Returns409()
    {
        var response = await _client.DeleteAsync("/api/app-users/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_MissingUser_Returns404()
    {
        var response = await _client.DeleteAsync("/api/app-users/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Reset-password is an Admin-only AuthController action (POST /api/Auth/reset-password),
    // not an app-users endpoint — see ResetPasswordTests.
}
