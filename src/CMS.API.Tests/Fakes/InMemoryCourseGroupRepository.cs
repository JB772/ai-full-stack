using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Stands in for <see cref="CourseGroupRepository"/> so the API endpoints can be exercised end-to-end
/// without a SQL Server instance. Mirrors the SQL semantics: pkid is a smallint IDENTITY, keyword LIKE on
/// Description, default sort (Description ASC, pkid ASC), and CourseCount / PartnerCourseGroupCount as the
/// delete guard.
///
/// Note what this fake deliberately does NOT model: the real FK_Course_CourseGroup is ON DELETE CASCADE, so
/// the database would let DeleteAsync succeed and take the courses with it. There is no cascade to reproduce
/// here, which is precisely why the 409 must be enforced in the controller — and why the tests assert it there.
/// </summary>
public class InMemoryCourseGroupRepository : ICourseGroupRepository
{
    private readonly List<CourseGroup> _groups =
    [
        // Unreferenced — safe to delete.
        new() { Pkid = 1, Description = "雲端", CourseCount = 0, PartnerCourseGroupCount = 0 },
        // Has courses — the cascade case; deleting this must be blocked.
        new() { Pkid = 2, Description = "資料庫", CourseCount = 12, PartnerCourseGroupCount = 3 },
        // Only PartnerCourseGroup rows — blocked by the non-cascading FK.
        new() { Pkid = 3, Description = "資訊安全", CourseCount = 0, PartnerCourseGroupCount = 2 }
    ];

    /// <summary>Stands in for the IDENTITY seed — the next generated key follows the seeded rows.</summary>
    private short _nextPkid = 4;

    public Task<IEnumerable<CourseGroup>> GetAllAsync()
        => Task.FromResult<IEnumerable<CourseGroup>>(Sorted(_groups).ToList());

    public Task<IEnumerable<CourseGroup>> QueryAsync(CourseGroupQuery query)
    {
        IEnumerable<CourseGroup> result = _groups;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            result = result.Where(g => g.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IEnumerable<CourseGroup>>(Sorted(result).ToList());
    }

    public Task<CourseGroup?> GetByPkidAsync(short pkid)
    {
        var group = _groups.SingleOrDefault(g => g.Pkid == pkid);
        return Task.FromResult(group is null ? null : Clone(group));
    }

    public Task<short> CreateAsync(CourseGroupRequest request)
    {
        // pkid is IDENTITY-generated — whatever the caller sent is ignored.
        var pkid = _nextPkid++;

        _groups.Add(new CourseGroup
        {
            Pkid = pkid,
            Description = request.Description,
            CourseCount = 0,
            PartnerCourseGroupCount = 0
        });

        return Task.FromResult(pkid);
    }

    public Task<bool> UpdateAsync(CourseGroupRequest request)
    {
        var group = _groups.SingleOrDefault(g => g.Pkid == request.Pkid);
        if (group is null)
        {
            return Task.FromResult(false);
        }

        // pkid identifies the row — not updatable. Description is the only writable column.
        group.Description = request.Description;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(short pkid)
    {
        var group = _groups.SingleOrDefault(g => g.Pkid == pkid);
        return Task.FromResult(group is not null && _groups.Remove(group));
    }

    private static IEnumerable<CourseGroup> Sorted(IEnumerable<CourseGroup> groups)
        => groups.OrderBy(g => g.Description, StringComparer.Ordinal)
                 .ThenBy(g => g.Pkid)
                 .Select(Clone);

    private static CourseGroup Clone(CourseGroup g) => new()
    {
        Pkid = g.Pkid,
        Description = g.Description,
        CourseCount = g.CourseCount,
        PartnerCourseGroupCount = g.PartnerCourseGroupCount
    };
}
