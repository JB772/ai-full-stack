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

    /// <summary>將密碼重設為 SysConfig 中的預設密碼並更新 PasswordUpdatedTime。找不到使用者回傳 false。</summary>
    Task<bool> ResetPasswordAsync(int pkid);
}
