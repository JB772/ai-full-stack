using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IPublishStatusRepository
{
    Task<IEnumerable<PublishStatus>> GetAllAsync();
    Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query);
    Task<PublishStatus?> GetByPkidAsync(byte pkid);
    Task<bool> PkidExistsAsync(byte pkid);

    /// <summary>Returns the caller-supplied pkid — the column is not an IDENTITY.</summary>
    Task<byte> CreateAsync(PublishStatusRequest request);

    Task<bool> UpdateAsync(PublishStatusRequest request);
    Task<bool> DeleteAsync(byte pkid);
}
