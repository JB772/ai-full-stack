using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAppUserRepository
{
    Task<IEnumerable<AppUser>> GetAllAsync();
    Task<IEnumerable<AppUser>> QueryAsync(AppUserQuery query);
    Task<AppUser?> GetByPkidAsync(int pkid);
    Task<bool> UserIdExistsAsync(string userId, int? excludePkid = null);
    Task<int> CreateAsync(AppUserRequest request);
    Task<bool> UpdateAsync(AppUserRequest request);
    Task<bool> DeleteAsync(int pkid);
    Task<int> GetRoleCountAsync(int pkid);

    /// <summary>取得使用者目前的角色清單 (含角色名稱)，依權限等級排序。</summary>
    Task<IEnumerable<UserRole>> GetRolesAsync(int pkid);

    /// <summary>該角色代碼是否存在於 AppRole。</summary>
    Task<bool> RoleExistsAsync(string roleId);

    /// <summary>使用者是否已被指派該角色。</summary>
    Task<bool> HasRoleAsync(int pkid, string roleId);

    /// <summary>指派角色給使用者 (寫入 AppUserRole + 稽核)。呼叫端須先確認使用者/角色存在且尚未指派。</summary>
    Task AssignRoleAsync(int pkid, string roleId);

    /// <summary>移除使用者的角色 (刪除 AppUserRole + 稽核)。回傳是否確有刪除。</summary>
    Task<bool> RemoveRoleAsync(int pkid, string roleId);
}
