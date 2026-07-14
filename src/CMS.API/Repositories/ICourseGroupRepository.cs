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
    Task<bool> DeleteAsync(short pkid);
}
