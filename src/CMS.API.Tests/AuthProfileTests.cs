using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for PUT /api/Auth/profile. The endpoint updates the UserName of the JWT user only —
/// UserId comes from the token (never the body) and roles are not changeable here. Requests use a real
/// signed token from the factory; a bare client is rejected by the global auth policy.
/// </summary>
public class AuthProfileTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task UpdateProfile_UpdatesUserName_ForJwtUser()
    {
        var client = _factory.CreateAuthenticatedClient("editor", "編輯者", "Editor");

        var response = await client.PutAsJsonAsync("/api/Auth/profile",
            new UpdateProfileRequest { UserName = "  新編輯者  " });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ProfileResponse>();

        Assert.NotNull(body);
        Assert.Equal("editor", body!.UserId);
        Assert.Equal("新編輯者", body.UserName); // trimmed
        // Roles are returned for display but are not affected by the update.
        Assert.Equal(new[] { "Editor" }, body.Roles);
    }

    [Fact]
    public async Task UpdateProfile_IgnoresUserIdInBody_UpdatesOnlyJwtUser()
    {
        // Authenticated as "editor" but the body tries to target "admin".
        var client = _factory.CreateAuthenticatedClient("editor", "編輯者", "Editor");

        var response = await client.PutAsJsonAsync("/api/Auth/profile",
            new UpdateProfileRequest { UserName = "冒充者", UserId = "admin" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ProfileResponse>();

        // The JWT user (editor) was updated, NOT the "admin" named in the body.
        Assert.Equal("editor", body!.UserId);
        Assert.Equal("冒充者", body.UserName);

        // Prove admin was untouched: logging in as admin still returns the original name.
        var anonymous = _factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/Auth/login",
            new LoginRequest { UserId = "admin", Password = "P@ssw0rd" });
        var adminProfile = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.Equal("系統管理員", adminProfile!.UserName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateProfile_RejectsEmptyOrWhitespaceUserName_With400(string userName)
    {
        var client = _factory.CreateAuthenticatedClient("editor", "編輯者", "Editor");

        var response = await client.PutAsJsonAsync("/api/Auth/profile",
            new UpdateProfileRequest { UserName = userName });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient(); // no Authorization header

        var response = await client.PutAsJsonAsync("/api/Auth/profile",
            new UpdateProfileRequest { UserName = "任何名稱" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
