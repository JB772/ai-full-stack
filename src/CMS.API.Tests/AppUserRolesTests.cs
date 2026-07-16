using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for the AppUserRole membership editor on /api/app-users/{id}/roles — the app's
/// first N-N role-assignment feature. Authorization is enforced on the server (Admin role), not merely
/// hidden in the UI: a non-Admin caller gets 403 and nothing changes. The in-memory fake seeds
/// admin (pkid 1) with Admin + User and guest (pkid 2) with none; known roles are Admin/User/browser.
/// </summary>
public class AppUserRolesTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _client; // Admin by default

    public AppUserRolesTests() => _client = _factory.CreateAuthenticatedClient("admin", "系統管理員", "Admin");

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private HttpClient NonAdminClient() => _factory.CreateAuthenticatedClient("guest", "訪客", "User");

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, int pkid, string roleId) =>
        client.PostAsJsonAsync($"/api/app-users/{pkid}/roles", new AssignRoleRequest { RoleId = roleId });

    // ---------- Read ----------

    [Fact]
    public async Task GetRoles_ExistingUser_ReturnsAssignedRolesWithNames()
    {
        var roles = await _client.GetFromJsonAsync<List<UserRole>>("/api/app-users/1/roles");

        Assert.NotNull(roles);
        Assert.Equal(["Admin", "User"], roles!.Select(r => r.RoleId));
        Assert.Equal("Administrator", roles[0].RoleName);
    }

    [Fact]
    public async Task GetRoles_UserWithNoRoles_ReturnsEmpty()
    {
        var roles = await _client.GetFromJsonAsync<List<UserRole>>("/api/app-users/2/roles");

        Assert.NotNull(roles);
        Assert.Empty(roles!);
    }

    [Fact]
    public async Task GetRoles_MissingUser_Returns404()
    {
        var response = await _client.GetAsync("/api/app-users/999/roles");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Assign ----------

    [Fact]
    public async Task AssignRole_ValidNewRole_Returns201_AndAppearsInList()
    {
        var response = await AssignAsync(_client, 2, "browser");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var roles = await response.Content.ReadFromJsonAsync<List<UserRole>>();
        Assert.Contains(roles!, r => r.RoleId == "browser");

        var fetched = await _client.GetFromJsonAsync<List<UserRole>>("/api/app-users/2/roles");
        Assert.Contains(fetched!, r => r.RoleId == "browser");
    }

    [Fact]
    public async Task AssignRole_IncrementsRoleCount()
    {
        Assert.Equal(0, (await _client.GetFromJsonAsync<AppUser>("/api/app-users/2"))!.RoleCount);

        await AssignAsync(_client, 2, "browser");

        Assert.Equal(1, (await _client.GetFromJsonAsync<AppUser>("/api/app-users/2"))!.RoleCount);
    }

    [Fact]
    public async Task AssignRole_AlreadyAssigned_Returns409()
    {
        var response = await AssignAsync(_client, 1, "Admin");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_UnknownRole_Returns404()
    {
        var response = await AssignAsync(_client, 2, "no-such-role");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_MissingUser_Returns404()
    {
        var response = await AssignAsync(_client, 999, "browser");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AssignRole_MissingRoleId_Returns400(string roleId)
    {
        var response = await AssignAsync(_client, 2, roleId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_AsNonAdmin_Returns403_AndChangesNothing()
    {
        var response = await AssignAsync(NonAdminClient(), 2, "browser");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var roles = await _client.GetFromJsonAsync<List<UserRole>>("/api/app-users/2/roles");
        Assert.Empty(roles!);
    }

    [Fact]
    public async Task AssignRole_WithoutToken_Returns401()
    {
        var anonymous = _factory.CreateClient();

        var response = await AssignAsync(anonymous, 2, "browser");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Remove ----------

    [Fact]
    public async Task RemoveRole_ExistingAssignment_Returns204_AndRemovedFromList()
    {
        var response = await _client.DeleteAsync("/api/app-users/1/roles/User");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var roles = await _client.GetFromJsonAsync<List<UserRole>>("/api/app-users/1/roles");
        Assert.DoesNotContain(roles!, r => r.RoleId == "User");
        Assert.Contains(roles!, r => r.RoleId == "Admin");
    }

    [Fact]
    public async Task RemoveRole_NotAssigned_Returns404()
    {
        var response = await _client.DeleteAsync("/api/app-users/1/roles/browser");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveRole_MissingUser_Returns404()
    {
        var response = await _client.DeleteAsync("/api/app-users/999/roles/Admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveRole_AsNonAdmin_Returns403_AndChangesNothing()
    {
        var response = await NonAdminClient().DeleteAsync("/api/app-users/1/roles/Admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var roles = await _client.GetFromJsonAsync<List<UserRole>>("/api/app-users/1/roles");
        Assert.Contains(roles!, r => r.RoleId == "Admin");
    }

    [Fact]
    public async Task RemoveRole_WithoutToken_Returns401()
    {
        var anonymous = _factory.CreateClient();

        var response = await anonymous.DeleteAsync("/api/app-users/1/roles/Admin");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
