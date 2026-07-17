using CMS.API.Models;

namespace CMS.API.Repositories;

public interface ICourseGroupRepository
{
    Task<IEnumerable<CourseGroup>> GetAllAsync();
    Task<IEnumerable<CourseGroup>> QueryAsync(CourseGroupQuery query);
    Task<CourseGroup?> GetByPkidAsync(short pkid);

    /// <summary>Returns the IDENTITY-generated pkid.</summary>
    Task<short> CreateAsync(CourseGroupRequest request);

    Task<bool> UpdateAsync(CourseGroupRequest request);

    /// <summary>
    /// Unguarded. FK_Course_CourseGroup is ON DELETE CASCADE, so SQL Server will delete every course in
    /// the group rather than refusing — the caller MUST check CourseCount / PartnerCourseGroupCount first.
    /// </summary>
    /// <summary>
    /// 刪除課程群組。回傳 <see cref="DeleteResult.Blocked"/> 表示交易內再檢查時仍有子資料
    /// (控制器的刪除前檢查與此為不同連線，而 FK 是 ON DELETE CASCADE — 見 docs/delete-guards.md)。
    /// </summary>
    Task<DeleteResult> DeleteAsync(short pkid);
}
