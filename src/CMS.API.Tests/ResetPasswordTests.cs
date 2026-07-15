using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using CMS.API.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for POST /api/Auth/reset-password — the Admin-only action that resets a target
/// account's password to the system default. Authorization is enforced on the server (role claim),
/// not merely hidden in the UI: a non-Admin caller gets 403 and nothing changes. On success the
/// stored hash becomes SHA256(defaultPassword) and PasswordUpdatedTime is stamped, with no password
/// or hash ever crossing the wire in either direction. Stored state is asserted via the in-memory
/// fake (the same singleton the app resolves) and, for the happy path, end-to-end by logging in with
/// the default password. The real SysConfig defaultPassword lookup lives only in the Dapper repo
/// (not covered here) — the fake supplies a known <see cref="InMemoryAuthRepository.DefaultPassword"/>.
/// </summary>
public class ResetPasswordTests : IDisposable
{
    private const string TargetUserId = "editor";
    private const string TargetOldPassword = "editor-pass";

    private readonly CmsApiFactory _factory = new();

    /// <summary>The very same in-memory repository instance the app resolves (registered as a singleton).</summary>
    private InMemoryAuthRepository Repo =>
        (InMemoryAuthRepository)_factory.Services.GetRequiredService<IAuthRepository>();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private HttpClient AdminClient() => _factory.CreateAuthenticatedClient("admin", "系統管理員", "Admin");
    private HttpClient NonAdminClient() => _factory.CreateAuthenticatedClient("editor", "編輯者", "Editor");

    private static Task<HttpResponseMessage> ResetAsync(HttpClient client, string userId) =>
        client.PostAsJsonAsync("/api/Auth/reset-password", new ResetPasswordRequest { UserId = userId });

    private async Task<bool> CanLoginAsync(string userId, string password)
    {
        var anonymous = _factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/Auth/login",
            new LoginRequest { UserId = userId, Password = password });
        return response.StatusCode == HttpStatusCode.OK;
    }

    // ---------- Authorization: Admin role is enforced on the server ----------

    [Fact]
    public async Task ResetPassword_AsNonAdmin_Returns403_AndChangesNothing()
    {
        var originalHash = Repo.GetStoredPasswordHash(TargetUserId);

        var response = await ResetAsync(NonAdminClient(), TargetUserId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Nothing was written: the target's hash and (absent) update time are untouched.
        Assert.Equal(originalHash, Repo.GetStoredPasswordHash(TargetUserId));
        Assert.Null(Repo.GetPasswordUpdatedTime(TargetUserId));
        Assert.True(await CanLoginAsync(TargetUserId, TargetOldPassword));
    }

    [Fact]
    public async Task ResetPassword_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient(); // no Authorization header

        var response = await ResetAsync(client, TargetUserId);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Admin success: hash becomes SHA256(default), time stamped, no hash leaked ----------

    [Fact]
    public async Task ResetPassword_AsAdmin_SetsHashToDefault_StampsTime_AndNeverLeaksHash()
    {
        Assert.Null(Repo.GetPasswordUpdatedTime(TargetUserId));

        var response = await ResetAsync(AdminClient(), TargetUserId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Stored hash is exactly SHA256(default password) (uppercase hex, via PasswordHasher).
        var expectedHash = PasswordHasher.Hash(InMemoryAuthRepository.DefaultPassword);
        Assert.Equal(expectedHash, Repo.GetStoredPasswordHash(TargetUserId));

        // PasswordUpdatedTime was stamped.
        Assert.NotNull(Repo.GetPasswordUpdatedTime(TargetUserId));

        // End-to-end: the default password now authenticates and the old one no longer does.
        Assert.True(await CanLoginAsync(TargetUserId, InMemoryAuthRepository.DefaultPassword));
        Assert.False(await CanLoginAsync(TargetUserId, TargetOldPassword));

        // The response carries no password/hash (204, empty body).
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal(string.Empty, raw);
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(expectedHash, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InMemoryAuthRepository.DefaultPassword, raw, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Validation ----------

    [Fact]
    public async Task ResetPassword_UnknownUser_Returns404()
    {
        var response = await ResetAsync(AdminClient(), "no-such-user");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResetPassword_MissingUserId_Returns400(string userId)
    {
        var response = await ResetAsync(AdminClient(), userId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
