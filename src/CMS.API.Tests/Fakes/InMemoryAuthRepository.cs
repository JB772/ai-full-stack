using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Stands in for <see cref="AuthRepository"/> so the login endpoint can be exercised without SQL
/// Server (or a SysConfig row). Mirrors the SQL credential check: UserId match AND IsActive = 1 AND
/// PasswordHash equal to SHA-256(password). Stored hashes are produced by the real
/// <see cref="PasswordHasher"/>, so the controller's hash-then-compare path is genuinely exercised.
/// The signing key stands in for SysConfig['appConfig'].symmetricSecurityKey and is long enough
/// (&gt;= 32 bytes) for HMAC-SHA256.
/// </summary>
public class InMemoryAuthRepository : IAuthRepository
{
    public const string SigningKey = "test-jwt-signing-key-0123456789-abcdefghijklmnop";

    /// <summary>
    /// Stands in for SysConfig['appConfig'].defaultPassword. The real Dapper repository reads this from
    /// SysConfig (not covered by dotnet test); the fake exposes a known value so reset-password tests can
    /// assert the stored hash equals SHA256(default).
    /// </summary>
    public const string DefaultPassword = "Cms@Default1";

    private sealed record Account(
        string UserId, string UserName, bool IsActive, string PasswordHash, string[] RoleIds,
        DateTime? PasswordUpdatedTime = null);

    private readonly List<Account> _accounts =
    [
        new("admin", "系統管理員", IsActive: true, PasswordHasher.Hash("P@ssw0rd"), ["Admin", "Editor"]),
        new("editor", "編輯者", IsActive: true, PasswordHasher.Hash("editor-pass"), ["Editor"]),
        // Correct password but disabled — must still fail with 401.
        new("disabled", "停用帳號", IsActive: false, PasswordHasher.Hash("still-correct"), ["Viewer"]),
    ];

    /// <summary>Test inspector: the currently stored PasswordHash for a user (null if unknown).</summary>
    public string? GetStoredPasswordHash(string userId) =>
        _accounts.SingleOrDefault(a => a.UserId == userId)?.PasswordHash;

    /// <summary>Test inspector: when the password was last updated (null if never / unknown user).</summary>
    public DateTime? GetPasswordUpdatedTime(string userId) =>
        _accounts.SingleOrDefault(a => a.UserId == userId)?.PasswordUpdatedTime;

    public Task<AuthenticatedUser?> AuthenticateAsync(string userId, string passwordHash)
    {
        var account = _accounts.SingleOrDefault(a =>
            a.UserId == userId && a.IsActive && a.PasswordHash == passwordHash);

        return Task.FromResult(account is null
            ? null
            : new AuthenticatedUser
            {
                UserId = account.UserId,
                UserName = account.UserName,
                RoleIds = account.RoleIds
            });
    }

    public Task<string> GetSigningKeyAsync() => Task.FromResult(SigningKey);

    public Task<AuthenticatedUser?> UpdateUserNameAsync(string userId, string userName)
    {
        var index = _accounts.FindIndex(a => a.UserId == userId);
        if (index < 0)
        {
            return Task.FromResult<AuthenticatedUser?>(null);
        }

        var updated = _accounts[index] with { UserName = userName };
        _accounts[index] = updated;

        return Task.FromResult<AuthenticatedUser?>(new AuthenticatedUser
        {
            UserId = updated.UserId,
            UserName = updated.UserName,
            RoleIds = updated.RoleIds
        });
    }

    public Task<bool> UpdatePasswordAsync(string userId, string newPasswordHash)
    {
        var index = _accounts.FindIndex(a => a.UserId == userId);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        // Mirror the real UPDATE: swap the stored hash and stamp the update time (real repo uses GETDATE()).
        _accounts[index] = _accounts[index] with
        {
            PasswordHash = newPasswordHash,
            PasswordUpdatedTime = DateTime.UtcNow
        };
        return Task.FromResult(true);
    }

    public Task<bool> ResetPasswordToDefaultAsync(string userId)
    {
        var index = _accounts.FindIndex(a => a.UserId == userId);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        // Mirror the real UPDATE: hash the (known) default password, swap it in and stamp the update time.
        _accounts[index] = _accounts[index] with
        {
            PasswordHash = PasswordHasher.Hash(DefaultPassword),
            PasswordUpdatedTime = DateTime.UtcNow
        };
        return Task.FromResult(true);
    }
}
