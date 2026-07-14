using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Stands in for <see cref="PublishStatusRepository"/> so the API endpoints can be exercised
/// end-to-end without a SQL Server instance. Mirrors the SQL semantics: pkid is caller-supplied
/// (not an IDENTITY) and unique, keyword LIKE on Description, tri-state bool filters,
/// pkid immutable on update, and CourseCount / PromotionCount as the delete guard.
/// </summary>
public class InMemoryPublishStatusRepository : IPublishStatusRepository
{
    private readonly List<PublishStatus> _statuses =
    [
        new() { Pkid = 1, Description = "草稿", IsDraft = true, CourseCount = 0, PromotionCount = 0 },
        new() { Pkid = 2, Description = "已發布", IsPublished = true, CourseCount = 5, PromotionCount = 2 },
        new() { Pkid = 3, Description = "已下架", IsDiscontinued = true, CourseCount = 0, PromotionCount = 0 }
    ];

    public Task<IEnumerable<PublishStatus>> GetAllAsync()
        => Task.FromResult<IEnumerable<PublishStatus>>(
            _statuses.OrderBy(s => s.Pkid).Select(Clone).ToList());

    public Task<IEnumerable<PublishStatus>> QueryAsync(PublishStatusQuery query)
    {
        IEnumerable<PublishStatus> result = _statuses;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            result = result.Where(s => s.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.IsDraft.HasValue)
        {
            result = result.Where(s => s.IsDraft == query.IsDraft.Value);
        }

        if (query.IsPublished.HasValue)
        {
            result = result.Where(s => s.IsPublished == query.IsPublished.Value);
        }

        if (query.IsDiscontinued.HasValue)
        {
            result = result.Where(s => s.IsDiscontinued == query.IsDiscontinued.Value);
        }

        return Task.FromResult<IEnumerable<PublishStatus>>(
            result.OrderBy(s => s.Pkid).Select(Clone).ToList());
    }

    public Task<PublishStatus?> GetByPkidAsync(byte pkid)
    {
        var status = _statuses.SingleOrDefault(s => s.Pkid == pkid);
        return Task.FromResult(status is null ? null : Clone(status));
    }

    public Task<bool> PkidExistsAsync(byte pkid)
        => Task.FromResult(_statuses.Any(s => s.Pkid == pkid));

    public Task<byte> CreateAsync(PublishStatusRequest request)
    {
        // pkid comes from the request — there is no identity counter to advance.
        _statuses.Add(new PublishStatus
        {
            Pkid = request.Pkid,
            Description = request.Description,
            IsDraft = request.IsDraft,
            IsPublished = request.IsPublished,
            IsDiscontinued = request.IsDiscontinued,
            CourseCount = 0,
            PromotionCount = 0
        });

        return Task.FromResult(request.Pkid);
    }

    public Task<bool> UpdateAsync(PublishStatusRequest request)
    {
        var status = _statuses.SingleOrDefault(s => s.Pkid == request.Pkid);
        if (status is null)
        {
            return Task.FromResult(false);
        }

        // pkid identifies the row — not updatable.
        status.Description = request.Description;
        status.IsDraft = request.IsDraft;
        status.IsPublished = request.IsPublished;
        status.IsDiscontinued = request.IsDiscontinued;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(byte pkid)
    {
        var status = _statuses.SingleOrDefault(s => s.Pkid == pkid);
        return Task.FromResult(status is not null && _statuses.Remove(status));
    }

    private static PublishStatus Clone(PublishStatus s) => new()
    {
        Pkid = s.Pkid,
        Description = s.Description,
        IsDraft = s.IsDraft,
        IsPublished = s.IsPublished,
        IsDiscontinued = s.IsDiscontinued,
        CourseCount = s.CourseCount,
        PromotionCount = s.PromotionCount
    };
}
