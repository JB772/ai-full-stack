using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Stands in for <see cref="CourseRepository"/> so the API endpoints can be exercised end-to-end
/// without SQL Server. Mirrors the SQL semantics: pkid is an int IDENTITY the DB generates; the three
/// FK nav objects (Partner / CourseGroup / PublishStatus) that the real query multi-maps are rebuilt
/// here from small label maps; keyword LIKE across the five short string columns; exact-match FK and
/// tri-state CanRepeat filters; inclusive ScheduleOn / ScheduleOff ranges; default sort by
/// DisplayOrder then pkid; and the six child counts as the delete guard.
///
/// The seed covers each delete-guard branch: 1 is blocked (FAQ + certifications), 2 is free to delete
/// (and is the one with a NULL CourseGroup), 3 is blocked (hot-course rows).
/// </summary>
public class InMemoryCourseRepository : ICourseRepository
{
    private static readonly Dictionary<short, string> Partners = new()
    {
        [1] = "Microsoft", [2] = "Cisco", [3] = "Oracle"
    };

    private static readonly Dictionary<short, string> CourseGroups = new()
    {
        [1] = "雲端服務", [2] = "程式開發"
    };

    private static readonly Dictionary<byte, string> PublishStatuses = new()
    {
        [1] = "草稿", [2] = "已上架"
    };

    private readonly List<Course> _courses =
    [
        new()
        {
            Pkid = 1, Title = "Azure 基礎", OfficialTitle = "Microsoft Azure Fundamentals",
            CourseId = "AZ-900", ProdCourseId = "PROD-AZ900", FriendlyUrl = "azure-fundamentals",
            DisplayOrder = 1, PartnerPkid = 1, CourseGroupPkid = 1, PublishStatusPkid = 2,
            ScheduleOn = new DateOnly(2026, 1, 1), ScheduleOff = new DateOnly(2036, 1, 1),
            Hour = 12, ListPrice = 8000m, LearningCredit = 3.5m, CanRepeat = true,
            CourseFaqCount = 1, CertificationCount = 2
        },
        new()
        {
            Pkid = 2, Title = "CCNA 認證課程", OfficialTitle = null,
            CourseId = "CCNA", ProdCourseId = "PROD-CCNA", FriendlyUrl = "ccna",
            DisplayOrder = 2, PartnerPkid = 2, CourseGroupPkid = null, PublishStatusPkid = 1,
            ScheduleOn = new DateOnly(2026, 3, 1), ScheduleOff = new DateOnly(2027, 3, 1),
            Hour = 40, ListPrice = 25000m, LearningCredit = 10.0m, CanRepeat = false
        },
        new()
        {
            Pkid = 3, Title = "Java SE 程式設計", OfficialTitle = "Oracle Java SE",
            CourseId = "JAVA-SE", ProdCourseId = "PROD-JAVASE", FriendlyUrl = "java-se",
            DisplayOrder = 3, PartnerPkid = 3, CourseGroupPkid = 2, PublishStatusPkid = 2,
            ScheduleOn = new DateOnly(2026, 6, 1), ScheduleOff = new DateOnly(2036, 6, 1),
            Hour = 30, ListPrice = 18000m, LearningCredit = 8.0m, CanRepeat = true,
            HotCourseCount = 3
        }
    ];

    /// <summary>Stands in for the IDENTITY counter: the next pkid SQL Server would hand out.</summary>
    private int _nextPkid = 4;

    public Task<IEnumerable<Course>> GetAllAsync()
        => Task.FromResult<IEnumerable<Course>>(Sorted(_courses).ToList());

    public Task<IEnumerable<Course>> QueryAsync(CourseQuery query)
    {
        IEnumerable<Course> result = _courses;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            result = result.Where(c =>
                c.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || (c.OfficialTitle?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || c.CourseId.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || c.ProdCourseId.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || c.FriendlyUrl.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.PartnerPkid.HasValue)
        {
            result = result.Where(c => c.PartnerPkid == query.PartnerPkid.Value);
        }

        if (query.CourseGroupPkid.HasValue)
        {
            result = result.Where(c => c.CourseGroupPkid == query.CourseGroupPkid.Value);
        }

        if (query.PublishStatusPkid.HasValue)
        {
            result = result.Where(c => c.PublishStatusPkid == query.PublishStatusPkid.Value);
        }

        if (query.ScheduleOnFrom.HasValue)
        {
            result = result.Where(c => c.ScheduleOn >= query.ScheduleOnFrom.Value);
        }

        if (query.ScheduleOnTo.HasValue)
        {
            result = result.Where(c => c.ScheduleOn <= query.ScheduleOnTo.Value);
        }

        if (query.ScheduleOffFrom.HasValue)
        {
            result = result.Where(c => c.ScheduleOff >= query.ScheduleOffFrom.Value);
        }

        if (query.ScheduleOffTo.HasValue)
        {
            result = result.Where(c => c.ScheduleOff <= query.ScheduleOffTo.Value);
        }

        if (query.CanRepeat.HasValue)
        {
            result = result.Where(c => c.CanRepeat == query.CanRepeat.Value);
        }

        return Task.FromResult<IEnumerable<Course>>(Sorted(result).ToList());
    }

    public Task<Course?> GetByPkidAsync(int pkid)
    {
        var course = _courses.SingleOrDefault(c => c.Pkid == pkid);
        return Task.FromResult(course is null ? null : Project(course));
    }

    public Task<int> CreateAsync(CourseRequest request)
    {
        // The caller's pkid is ignored — the "database" assigns it.
        var pkid = _nextPkid++;

        _courses.Add(new Course
        {
            Pkid = pkid,
            Title = request.Title,
            OfficialTitle = request.OfficialTitle,
            CourseId = request.CourseId,
            ProdCourseId = request.ProdCourseId,
            FriendlyUrl = request.FriendlyUrl,
            DisplayOrder = request.DisplayOrder,
            PartnerPkid = request.PartnerPkid,
            CourseGroupPkid = request.CourseGroupPkid,
            PublishStatusPkid = request.PublishStatusPkid,
            ScheduleOn = request.ScheduleOn,
            ScheduleOff = request.ScheduleOff,
            Hour = request.Hour,
            ListPrice = request.ListPrice,
            LearningCredit = request.LearningCredit,
            Material = request.Material,
            Objective = request.Objective,
            Target = request.Target,
            Prerequisites = request.Prerequisites,
            Outline = request.Outline,
            TowardCertOrExam = request.TowardCertOrExam,
            Note = request.Note,
            OtherInfo = request.OtherInfo,
            CanRepeat = request.CanRepeat
            // A brand-new course has all six child counts at zero.
        });

        return Task.FromResult(pkid);
    }

    public Task<bool> UpdateAsync(CourseRequest request)
    {
        var course = _courses.SingleOrDefault(c => c.Pkid == request.Pkid);
        if (course is null)
        {
            return Task.FromResult(false);
        }

        // pkid identifies the row — not updatable. Every other column is writable.
        course.Title = request.Title;
        course.OfficialTitle = request.OfficialTitle;
        course.CourseId = request.CourseId;
        course.ProdCourseId = request.ProdCourseId;
        course.FriendlyUrl = request.FriendlyUrl;
        course.DisplayOrder = request.DisplayOrder;
        course.PartnerPkid = request.PartnerPkid;
        course.CourseGroupPkid = request.CourseGroupPkid;
        course.PublishStatusPkid = request.PublishStatusPkid;
        course.ScheduleOn = request.ScheduleOn;
        course.ScheduleOff = request.ScheduleOff;
        course.Hour = request.Hour;
        course.ListPrice = request.ListPrice;
        course.LearningCredit = request.LearningCredit;
        course.Material = request.Material;
        course.Objective = request.Objective;
        course.Target = request.Target;
        course.Prerequisites = request.Prerequisites;
        course.Outline = request.Outline;
        course.TowardCertOrExam = request.TowardCertOrExam;
        course.Note = request.Note;
        course.OtherInfo = request.OtherInfo;
        course.CanRepeat = request.CanRepeat;
        return Task.FromResult(true);
    }

    /// <summary>
    /// Test seam for the TOCTOU window. Invoked inside <see cref="DeleteAsync"/> immediately before its
    /// guard runs — exactly where a concurrent INSERT lands in production: after the controller's
    /// pre-check has already passed on a different connection, but before the delete transaction reads
    /// the counts. Mutate the supplied row to simulate that insert.
    /// </summary>
    public Action<Course>? OnBeforeDeleteGuard { get; set; }

    public Task<DeleteResult> DeleteAsync(int pkid)
    {
        var course = _courses.SingleOrDefault(c => c.Pkid == pkid);
        if (course is null)
        {
            return Task.FromResult(DeleteResult.NotFound);
        }

        OnBeforeDeleteGuard?.Invoke(course);

        // Mirrors the real repository's in-transaction re-check. CourseInCertification and
        // CourseJobCategories are ON DELETE CASCADE in the real schema, so this guard is the only
        // thing standing between a delete and silent destruction of the junction rows.
        if (course.CourseFaqCount > 0 || course.CertificationCount > 0 || course.JobCategoryCount > 0
            || course.RelatedLinkCount > 0 || course.HotCourseCount > 0 || course.RecommCount > 0)
        {
            return Task.FromResult(DeleteResult.Blocked);
        }

        _courses.Remove(course);
        return Task.FromResult(DeleteResult.Deleted);
    }

    private static IEnumerable<Course> Sorted(IEnumerable<Course> courses)
        => courses.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Pkid).Select(Project);

    /// <summary>Clones the row and rebuilds the nav objects the real multi-map query would JOIN in.</summary>
    private static Course Project(Course c) => new()
    {
        Pkid = c.Pkid,
        Title = c.Title,
        OfficialTitle = c.OfficialTitle,
        CourseId = c.CourseId,
        ProdCourseId = c.ProdCourseId,
        FriendlyUrl = c.FriendlyUrl,
        DisplayOrder = c.DisplayOrder,
        PartnerPkid = c.PartnerPkid,
        CourseGroupPkid = c.CourseGroupPkid,
        PublishStatusPkid = c.PublishStatusPkid,
        ScheduleOn = c.ScheduleOn,
        ScheduleOff = c.ScheduleOff,
        Hour = c.Hour,
        ListPrice = c.ListPrice,
        LearningCredit = c.LearningCredit,
        Material = c.Material,
        Objective = c.Objective,
        Target = c.Target,
        Prerequisites = c.Prerequisites,
        Outline = c.Outline,
        TowardCertOrExam = c.TowardCertOrExam,
        Note = c.Note,
        OtherInfo = c.OtherInfo,
        CanRepeat = c.CanRepeat,
        Partner = new CourseNavPartner
        {
            Pkid = c.PartnerPkid,
            Name = Partners.GetValueOrDefault(c.PartnerPkid, $"原廠{c.PartnerPkid}")
        },
        CourseGroup = c.CourseGroupPkid is { } groupPkid
            ? new CourseNavCourseGroup
            {
                Pkid = groupPkid,
                Description = CourseGroups.GetValueOrDefault(groupPkid, $"群組{groupPkid}")
            }
            : null,
        PublishStatus = new CourseNavPublishStatus
        {
            Pkid = c.PublishStatusPkid,
            Description = PublishStatuses.GetValueOrDefault(c.PublishStatusPkid, $"狀態{c.PublishStatusPkid}")
        },
        CourseFaqCount = c.CourseFaqCount,
        CertificationCount = c.CertificationCount,
        JobCategoryCount = c.JobCategoryCount,
        RelatedLinkCount = c.RelatedLinkCount,
        HotCourseCount = c.HotCourseCount,
        RecommCount = c.RecommCount
    };
}
