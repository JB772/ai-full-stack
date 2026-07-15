using System.Net;
using System.Net.Http.Json;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using CMS.API.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace CMS.API.Tests;

/// <summary>
/// Endpoint tests for POST /api/Auth/change-password. Identity comes from the JWT (never the body).
/// The flow is: verify current password → enforce new-password complexity → new/confirm must match →
/// only then write the new hash and stamp PasswordUpdatedTime. No password hash is ever sent back.
/// Stored state is asserted via the in-memory fake (same singleton the running app uses) and, for the
/// happy path, end-to-end by logging in with the new password.
/// </summary>
public class ChangePasswordTests : IDisposable
{
    private const string UserId = "editor";
    private const string CurrentPassword = "editor-pass";

    private readonly CmsApiFactory _factory = new();

    /// <summary>The very same in-memory repository instance the app resolves (registered as a singleton).</summary>
    private InMemoryAuthRepository Repo =>
        (InMemoryAuthRepository)_factory.Services.GetRequiredService<IAuthRepository>();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record ErrorBody(string Message);

    private HttpClient EditorClient() => _factory.CreateAuthenticatedClient(UserId, "編輯者", "Editor");

    private Task<HttpResponseMessage> ChangeAsync(
        HttpClient client, string current, string next, string confirm) =>
        client.PostAsJsonAsync("/api/Auth/change-password",
            new ChangePasswordRequest
            {
                CurrentPassword = current,
                NewPassword = next,
                ConfirmNewPassword = confirm
            });

    private async Task<bool> CanLoginAsync(string password)
    {
        var anonymous = _factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/Auth/login",
            new LoginRequest { UserId = UserId, Password = password });
        return response.StatusCode == HttpStatusCode.OK;
    }

    // ---------- (1) Wrong current password changes nothing ----------

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_ChangesNothing()
    {
        var originalHash = Repo.GetStoredPasswordHash(UserId);

        var response = await ChangeAsync(EditorClient(), "not-the-current-password", "New-Pass1", "New-Pass1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Nothing was written: hash unchanged, no update timestamp.
        Assert.Equal(originalHash, Repo.GetStoredPasswordHash(UserId));
        Assert.Null(Repo.GetPasswordUpdatedTime(UserId));

        // And the account still authenticates only with the original password.
        Assert.True(await CanLoginAsync(CurrentPassword));
        Assert.False(await CanLoginAsync("New-Pass1"));
    }

    // ---------- (2) Complexity enforced ----------

    [Theory]
    [InlineData("Ab1!xy")]      // 4 classes but only 6 chars — too short
    [InlineData("Short1!")]     // 4 classes but 7 chars — too short
    [InlineData("abcdefgh")]    // 8 chars but 1 class (lowercase only)
    [InlineData("ABCDEFGH")]    // 8 chars but 1 class (uppercase only)
    [InlineData("abcdefg1")]    // 8 chars but 2 classes (lowercase + digit)
    public async Task ChangePassword_WeakNewPassword_Rejected_WithComplexityMessage(string weak)
    {
        var originalHash = Repo.GetStoredPasswordHash(UserId);

        var response = await ChangeAsync(EditorClient(), CurrentPassword, weak, weak);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal(PasswordPolicy.ComplexityMessage, body!.Message);

        // Rejected before any write.
        Assert.Equal(originalHash, Repo.GetStoredPasswordHash(UserId));
        Assert.Null(Repo.GetPasswordUpdatedTime(UserId));
    }

    // Pure-unit boundaries of the shared policy: exactly 8 chars + exactly 3 classes is the pass line.
    [Theory]
    [InlineData("Abc123!x", true)]   // 8 chars, 4 classes
    [InlineData("Abcdefg1", true)]   // 8 chars, 3 classes (upper+lower+digit)
    [InlineData("Abcdef1", false)]   // 7 chars — too short (even with 3 classes)
    [InlineData("abcdefg1", false)]  // 8 chars, 2 classes
    [InlineData("abcdefgh", false)]  // 8 chars, 1 class
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PasswordPolicy_IsComplexEnough_EnforcesLengthAndThreeClasses(string? password, bool expected)
    {
        Assert.Equal(expected, PasswordPolicy.IsComplexEnough(password));
    }

    // ---------- (3) New / confirm mismatch ----------

    [Fact]
    public async Task ChangePassword_NewAndConfirmMismatch_Rejected()
    {
        var originalHash = Repo.GetStoredPasswordHash(UserId);

        var response = await ChangeAsync(EditorClient(), CurrentPassword, "New-Pass1", "New-Pass2");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(originalHash, Repo.GetStoredPasswordHash(UserId));
        Assert.Null(Repo.GetPasswordUpdatedTime(UserId));
    }

    // ---------- (4) Valid change ----------

    [Fact]
    public async Task ChangePassword_ValidChange_SetsHashAndUpdatedTime_AndNeverLeaksHash()
    {
        const string newPassword = "New-Pass1";

        var response = await ChangeAsync(EditorClient(), CurrentPassword, newPassword, newPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Stored hash is exactly SHA256(new password) (uppercase hex, via PasswordHasher).
        Assert.Equal(PasswordHasher.Hash(newPassword), Repo.GetStoredPasswordHash(UserId));

        // PasswordUpdatedTime was set.
        Assert.NotNull(Repo.GetPasswordUpdatedTime(UserId));

        // End-to-end: the new password now authenticates and the old one no longer does.
        Assert.True(await CanLoginAsync(newPassword));
        Assert.False(await CanLoginAsync(CurrentPassword));

        // The response never carries any hash.
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PasswordHasher.Hash(newPassword), raw, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Auth guard ----------

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient(); // no Authorization header

        var response = await ChangeAsync(client, CurrentPassword, "New-Pass1", "New-Pass1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
