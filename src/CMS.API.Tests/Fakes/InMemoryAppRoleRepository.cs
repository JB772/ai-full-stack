using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Stands in for <see cref="AppRoleRepository"/> so the API endpoints can be exercised
/// end-to-end without a SQL Server instance. Mirrors the SQL semantics: RoleId uniqueness,
/// keyword LIKE across RoleId/RoleName/Description, PermissionLevel range, RoleId immutable on update.
/// </summary>
public class InMemoryAppRoleRepository : IAppRoleRepository
{
    private readonly List<AppRole> _roles = [];
    private int _nextPkid = 1;

    public InMemoryAppRoleRepository()
    {
        Seed(new AppRole { RoleId = "Admin", RoleName = "Administrator", PermissionLevel = 1, Description = "系統管理員", UserCount = 3 });
        Seed(new AppRole { RoleId = "User", RoleName = "User", PermissionLevel = 100, Description = "一般使用者", UserCount = 0 });
    }

    private void Seed(AppRole role)
    {
        role.Pkid = _nextPkid++;
        _roles.Add(role);
    }

    public Task<IEnumerable<AppRole>> GetAllAsync()
        => Task.FromResult<IEnumerable<AppRole>>(_roles.OrderBy(r => r.RoleId, StringComparer.OrdinalIgnoreCase).Select(Clone).ToList());

    public Task<IEnumerable<AppRole>> QueryAsync(AppRoleQuery query)
    {
        IEnumerable<AppRole> result = _roles;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            result = result.Where(r =>
                r.RoleId.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                r.RoleName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (r.Description ?? string.Empty).Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.PermissionLevelFrom.HasValue)
        {
            result = result.Where(r => r.PermissionLevel >= query.PermissionLevelFrom.Value);
        }

        if (query.PermissionLevelTo.HasValue)
        {
            result = result.Where(r => r.PermissionLevel <= query.PermissionLevelTo.Value);
        }

        return Task.FromResult<IEnumerable<AppRole>>(
            result.OrderBy(r => r.RoleId, StringComparer.OrdinalIgnoreCase).Select(Clone).ToList());
    }

    public Task<AppRole?> GetByPkidAsync(int pkid)
    {
        var role = _roles.SingleOrDefault(r => r.Pkid == pkid);
        return Task.FromResult(role is null ? null : Clone(role));
    }

    public Task<bool> RoleIdExistsAsync(string roleId, int? excludePkid = null)
        => Task.FromResult(_roles.Any(r =>
            r.RoleId.Equals(roleId, StringComparison.OrdinalIgnoreCase) &&
            (!excludePkid.HasValue || r.Pkid != excludePkid.Value)));

    public Task<int> CreateAsync(AppRoleRequest request)
    {
        var role = new AppRole
        {
            Pkid = _nextPkid++,
            RoleId = request.RoleId,
            RoleName = request.RoleName,
            PermissionLevel = request.PermissionLevel,
            Description = request.Description,
            UserCount = 0
        };
        _roles.Add(role);
        return Task.FromResult(role.Pkid);
    }

    public Task<bool> UpdateAsync(AppRoleRequest request)
    {
        var role = _roles.SingleOrDefault(r => r.Pkid == request.Pkid);
        if (role is null)
        {
            return Task.FromResult(false);
        }

        // RoleId is the natural key referenced by AppUserRole — not updatable.
        role.RoleName = request.RoleName;
        role.PermissionLevel = request.PermissionLevel;
        role.Description = request.Description;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int pkid)
    {
        var role = _roles.SingleOrDefault(r => r.Pkid == pkid);
        return Task.FromResult(role is not null && _roles.Remove(role));
    }

    public Task<int> GetUserCountAsync(int pkid)
        => Task.FromResult(_roles.SingleOrDefault(r => r.Pkid == pkid)?.UserCount ?? 0);

    private static AppRole Clone(AppRole r) => new()
    {
        Pkid = r.Pkid,
        RoleId = r.RoleId,
        RoleName = r.RoleName,
        PermissionLevel = r.PermissionLevel,
        Description = r.Description,
        UserCount = r.UserCount
    };
}
