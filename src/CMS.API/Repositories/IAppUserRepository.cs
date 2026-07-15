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
}
