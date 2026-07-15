using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CMS.API.Models;

namespace CMS.API.Tests;

/// <summary>
/// Cross-cutting authorization tests. Global policy requires an authenticated user on every endpoint
/// except AuthController, which is [AllowAnonymous]. Uses a real, signed token from the factory —
/// there is no auth bypass in the Testing environment.
/// </summary>
public class AuthorizationTests : IDisposable
{
    private readonly CmsApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutBearerToken_Returns401()
    {
        var client = _factory.CreateClient(); // no Authorization header

        var response = await client.GetAsync("/api/app-users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidBearerToken_Returns200()
    {
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/app-users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<AppUser>>();
        Assert.NotNull(users);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithGarbageBearerToken_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-real-token");

        var response = await client.GetAsync("/api/app-users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthController_Login_IsAnonymous_ReachableWithoutToken()
    {
        var client = _factory.CreateClient(); // no Authorization header

        var response = await client.PostAsJsonAsync("/api/Auth/login",
            new LoginRequest { UserId = "admin", Password = "P@ssw0rd" });

        // Not 401 — the endpoint is reachable anonymously and authenticates the credentials.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
    }

    [Fact]
    public async Task AuthController_Login_WithBadCredentials_Returns401_NotFromGlobalPolicy()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Auth/login",
            new LoginRequest { UserId = "admin", Password = "wrong" });

        // The anonymous endpoint is reached; the 401 comes from the credential check, not the auth gate.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
