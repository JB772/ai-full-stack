using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Stands in for <see cref="AppUserRepository"/> so the API endpoints can be exercised without SQL
/// Server (or a SysConfig row). Mirrors the SQL semantics: UserId uniqueness, keyword LIKE across
/// UserId/UserName, IsActive filter, UserId immutable on update, and PasswordHash never surfaced.
/// The default-password/SysConfig lookup lives only in the real Dapper repository, so it is
/// intentionally absent here — Create just seeds a new account. (Reset-to-default is an Admin-only
/// AuthController action keyed by UserId — see InMemoryAuthRepository — not an AppUser endpoint.)
///
/// AppUserRole membership is tracked in-memory (<see cref="_assignments"/>); RoleCount is derived
/// from it so it stays consistent after assign/remove. A small known-roles map mirrors
/// InMemoryAppRoleRepository so RoleExistsAsync/GetRolesAsync can resolve role names without SQL.
/// </summary>
public class InMemoryAppUserRepository : IAppUserRepository
{
    private readonly List<AppUser> _users = [];
    private readonly List<(string UserId, string RoleId)> _assignments = [];
    private int _nextPkid = 1;

    // Mirrors the AppRole seed in InMemoryAppRoleRepository (RoleId → RoleName).
    private static readonly Dictionary<string, string> KnownRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Admin"] = "Administrator",
        ["User"] = "User",
        ["browser"] = "browser"
    };

    public InMemoryAppUserRepository()
    {
        // admin has role assignments → delete is blocked (409). guest has none and is inactive.
        Seed(new AppUser { UserId = "admin", UserName = "系統管理員", IsActive = true, PasswordUpdatedTime = null });
        Seed(new AppUser { UserId = "guest", UserName = "訪客", IsActive = false, PasswordUpdatedTime = null });
        _assignments.Add(("admin", "Admin"));
        _assignments.Add(("admin", "User"));
    }

    private void Seed(AppUser user)
    {
        user.Pkid = _nextPkid++;
        _users.Add(user);
    }

    private int CountRoles(string userId)
        => _assignments.Count(a => a.UserId.Equals(userId, StringComparison.OrdinalIgnoreCase));

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
            PasswordUpdatedTime = null
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
    {
        var user = _users.SingleOrDefault(u => u.Pkid == pkid);
        return Task.FromResult(user is null ? 0 : CountRoles(user.UserId));
    }

    public Task<IEnumerable<UserRole>> GetRolesAsync(int pkid)
    {
        var user = _users.SingleOrDefault(u => u.Pkid == pkid);
        if (user is null)
        {
            return Task.FromResult<IEnumerable<UserRole>>([]);
        }

        var roles = _assignments
            .Where(a => a.UserId.Equals(user.UserId, StringComparison.OrdinalIgnoreCase))
            .Select(a => new UserRole { RoleId = a.RoleId, RoleName = KnownRoles.GetValueOrDefault(a.RoleId, a.RoleId) })
            .OrderBy(r => r.RoleId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IEnumerable<UserRole>>(roles);
    }

    public Task<bool> RoleExistsAsync(string roleId)
        => Task.FromResult(KnownRoles.ContainsKey(roleId));

    public Task<bool> HasRoleAsync(int pkid, string roleId)
    {
        var user = _users.SingleOrDefault(u => u.Pkid == pkid);
        return Task.FromResult(user is not null && _assignments.Any(a =>
            a.UserId.Equals(user.UserId, StringComparison.OrdinalIgnoreCase) &&
            a.RoleId.Equals(roleId, StringComparison.OrdinalIgnoreCase)));
    }

    public Task AssignRoleAsync(int pkid, string roleId)
    {
        var user = _users.SingleOrDefault(u => u.Pkid == pkid);
        if (user is not null)
        {
            _assignments.Add((user.UserId, roleId));
        }

        return Task.CompletedTask;
    }

    public Task<bool> RemoveRoleAsync(int pkid, string roleId)
    {
        var user = _users.SingleOrDefault(u => u.Pkid == pkid);
        if (user is null)
        {
            return Task.FromResult(false);
        }

        var removed = _assignments.RemoveAll(a =>
            a.UserId.Equals(user.UserId, StringComparison.OrdinalIgnoreCase) &&
            a.RoleId.Equals(roleId, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(removed > 0);
    }

    private AppUser Clone(AppUser u) => new()
    {
        Pkid = u.Pkid,
        UserId = u.UserId,
        UserName = u.UserName,
        IsActive = u.IsActive,
        PasswordUpdatedTime = u.PasswordUpdatedTime,
        RoleCount = CountRoles(u.UserId)
    };
}
