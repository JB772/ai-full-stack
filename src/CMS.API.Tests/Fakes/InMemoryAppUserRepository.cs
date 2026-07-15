using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Stands in for <see cref="AppUserRepository"/> so the API endpoints can be exercised without SQL
/// Server (or a SysConfig row). Mirrors the SQL semantics: UserId uniqueness, keyword LIKE across
/// UserId/UserName, IsActive filter, UserId immutable on update, PasswordHash never surfaced, and
/// reset-password stamping PasswordUpdatedTime. The default-password/SysConfig lookup lives only in
/// the real Dapper repository, so it is intentionally absent here — Create just seeds a new account.
/// </summary>
public class InMemoryAppUserRepository : IAppUserRepository
{
    private readonly List<AppUser> _users = [];
    private int _nextPkid = 1;

    public InMemoryAppUserRepository()
    {
        // admin has role assignments → delete is blocked (409). guest has none and is inactive.
        Seed(new AppUser { UserId = "admin", UserName = "系統管理員", IsActive = true, PasswordUpdatedTime = null, RoleCount = 2 });
        Seed(new AppUser { UserId = "guest", UserName = "訪客", IsActive = false, PasswordUpdatedTime = null, RoleCount = 0 });
    }

    private void Seed(AppUser user)
    {
        user.Pkid = _nextPkid++;
        _users.Add(user);
    }

    public Task<IEnumerable<AppUser>> GetAllAsync()
        => Task.FromResult<IEnumerable<AppUser>>(_users.OrderBy(u => u.UserId, StringComparer.OrdinalIgnoreCase).Select(Clone).ToList());

    public Task<IEnumerable<AppUser>> QueryAsync(AppUserQuery query)
    {
        IEnumerable<AppUser> result = _users;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            result = result.Where(u =>
                u.UserId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                u.UserName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.IsActive.HasValue)
        {
            result = result.Where(u => u.IsActive == query.IsActive.Value);
        }

        return Task.FromResult<IEnumerable<AppUser>>(
            result.OrderBy(u => u.UserId, StringComparer.OrdinalIgnoreCase).Select(Clone).ToList());
    }

    public Task<AppUser?> GetByPkidAsync(int pkid)
    {
        var user = _users.SingleOrDefault(u => u.Pkid == pkid);
        return Task.FromResult(user is null ? null : Clone(user));
    }

    public Task<bool> UserIdExistsAsync(string userId, int? excludePkid = null)
        => Task.FromResult(_users.Any(u =>
            u.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase) &&
            (!excludePkid.HasValue || u.Pkid != excludePkid.Value)));

    public Task<int> CreateAsync(AppUserRequest request)
    {
        var user = new AppUser
        {
            Pkid = _nextPkid++,
            UserId = request.UserId,
            UserName = request.UserName,
            IsActive = request.IsActive,
            PasswordUpdatedTime = null,
            RoleCount = 0
        };
        _users.Add(user);
        return Task.FromResult(user.Pkid);
    }

    public Task<bool> UpdateAsync(AppUserRequest request)
    {
        var user = _users.SingleOrDefault(u => u.Pkid == request.Pkid);
        if (user is null)
        {
            return Task.FromResult(false);
        }

        // UserId (natural key) and the password are not updatable here.
        user.UserName = request.UserName;
        user.IsActive = request.IsActive;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int pkid)
    {
        var user = _users.SingleOrDefault(u => u.Pkid == pkid);
        return Task.FromResult(user is not null && _users.Remove(user));
    }

    public Task<int> GetRoleCountAsync(int pkid)
        => Task.FromResult(_users.SingleOrDefault(u => u.Pkid == pkid)?.RoleCount ?? 0);

    public Task<bool> ResetPasswordAsync(int pkid)
    {
        var user = _users.SingleOrDefault(u => u.Pkid == pkid);
        if (user is null)
        {
            return Task.FromResult(false);
        }

        user.PasswordUpdatedTime = DateTime.Now;
        return Task.FromResult(true);
    }

    private static AppUser Clone(AppUser u) => new()
    {
        Pkid = u.Pkid,
        UserId = u.UserId,
        UserName = u.UserName,
        IsActive = u.IsActive,
        PasswordUpdatedTime = u.PasswordUpdatedTime,
        RoleCount = u.RoleCount
    };
}
