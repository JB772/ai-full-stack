using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;
using CMS.API.Security;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for POST /api/Auth/login. A fresh factory (with an in-memory auth repository seeded
/// with admin/editor/disabled accounts) is created per test. The response is a user profile plus a
/// signed JWT; PasswordHash is backend-only and appears nowhere in the response.
/// </summary>
public class AuthControllerTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();
    private readonly HttpClient _client;

    public AuthControllerTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<HttpResponseMessage> LoginAsync(string userId, string password)
        => _client.PostAsJsonAsync("/api/Auth/login", new LoginRequest { UserId = userId, Password = password });

    // ---------- Success ----------

    [Fact]
    public async Task Login_ValidActiveUser_ReturnsProfileWithToken()
    {
        var response = await LoginAsync("admin", "P@ssw0rd");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(body);
        Assert.Equal("admin", body!.UserId);
        Assert.Equal("系統管理員", body.UserName);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    // ---------- Failure (all return a generic 401) ----------

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await LoginAsync("admin", "not-the-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownUserId_Returns401()
    {
        var response = await LoginAsync("nobody", "P@ssw0rd");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_InactiveUser_WithCorrectPassword_Returns401()
    {
        // "disabled" has the right password but IsActive = 0.
        var response = await LoginAsync("disabled", "still-correct");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Token contents ----------

    [Fact]
    public async Task Login_IssuedToken_CarriesRoleClaimsAndDayLongExpiry()
    {
        var response = await LoginAsync("admin", "P@ssw0rd");
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var token = handler.ReadJwtToken(body!.AccessToken);

        // Every RoleId in AppUserRole becomes a role claim.
        var roles = token.Claims
            .Where(c => c.Type == JwtTokenService.RoleClaimType)
            .Select(c => c.Value)
            .OrderBy(r => r)
            .ToArray();
        Assert.Equal(new[] { "Admin", "Editor" }, roles);

        // UserId and UserName are present as claims.
        Assert.Contains(token.Claims, c => c.Type == JwtTokenService.UserIdClaimType && c.Value == "admin");
        Assert.Contains(token.Claims, c => c.Type == JwtTokenService.UserNameClaimType && c.Value == "系統管理員");

        // Token expires ~24 hours after issue.
        var lifetime = token.ValidTo - DateTime.UtcNow;
        Assert.InRange(lifetime.TotalHours, 23.5, 24.5);
    }

    [Fact]
    public async Task Login_Response_NeverContainsPasswordHash()
    {
        var response = await LoginAsync("admin", "P@ssw0rd");
        var raw = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PasswordHasher.Hash("P@ssw0rd"), raw, StringComparison.OrdinalIgnoreCase);
    }
}
