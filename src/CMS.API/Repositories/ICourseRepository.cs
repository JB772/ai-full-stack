using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICourseRepository
{
    Task<IEnumerable<Course>> GetAllAsync();
    Task<IEnumerable<Course>> QueryAsync(CourseQuery query);
    Task<Course?> GetByPkidAsync(int pkid);

    /// <summary>Returns the IDENTITY-generated pkid.</summary>
    Task<int> CreateAsync(CourseRequest request);

    Task<bool> UpdateAsync(CourseRequest request);
    /// <summary>
    /// 刪除課程。回傳 <see cref="DeleteResult.Blocked"/> 表示交易內再檢查時仍有子資料
    /// (控制器的刪除前檢查與此為不同連線，而部分 FK 是 ON DELETE CASCADE — 見 docs/delete-guards.md)。
    /// </summary>
    Task<DeleteResult> DeleteAsync(int pkid);
}
