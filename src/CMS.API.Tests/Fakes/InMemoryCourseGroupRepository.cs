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
/// the database would let the DELETE statement succeed and take the courses with it. There is no cascade to
/// reproduce here, which is precisely why the guard must be enforced in application code.
///
/// That guard lives in BOTH layers, and this fake models both: the controller pre-checks (fast path, friendly
/// message), and <see cref="DeleteAsync"/> re-checks and returns <see cref="DeleteResult.Blocked"/>. The
/// repository-side re-check is what closes the TOCTOU window between the two connections — without it a
/// Course inserted mid-window is silently cascaded away. Keep this fake's guard in step with the real one, or
/// the endpoint tests stop proving the contract.
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

    /// <summary>
    /// Test seam for the TOCTOU window. Invoked inside <see cref="DeleteAsync"/> immediately before its
    /// guard runs — exactly where a concurrent INSERT lands in production: after the controller's
    /// pre-check has already passed on a different connection, but before the delete transaction reads
    /// the counts. Mutate the supplied row to simulate that insert.
    /// </summary>
    public Action<CourseGroup>? OnBeforeDeleteGuard { get; set; }

    public Task<DeleteResult> DeleteAsync(short pkid)
    {
        var group = _groups.SingleOrDefault(g => g.Pkid == pkid);
        if (group is null)
        {
            return Task.FromResult(DeleteResult.NotFound);
        }

        OnBeforeDeleteGuard?.Invoke(group);

        // Mirrors the real repository's in-transaction re-check.
        if (group.CourseCount > 0 || group.PartnerCourseGroupCount > 0)
        {
            return Task.FromResult(DeleteResult.Blocked);
        }

        _groups.Remove(group);
        return Task.FromResult(DeleteResult.Deleted);
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
