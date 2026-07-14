using CMS.API.Models;
using CMS.API.Repositories;

namespace CMS.API.Tests.Fakes;

/// <summary>
/// Stands in for <see cref="PartnerRepository"/> so the API endpoints can be exercised end-to-end
/// without a SQL Server instance. Mirrors the SQL semantics: pkid is a smallint IDENTITY the DB
/// generates, keyword LIKE across the four short string columns, DisplayOrder range, HasImage
/// tri-state on the nullable ImageFilename, default sort by DisplayOrder then pkid, and the five
/// child counts as the delete guard.
///
/// The seed covers each delete-guard branch: 1 is blocked by courses + certifications, 2 is blocked
/// only by seminars (the child with no FK constraint in the schema), and 3 is free to delete.
/// </summary>
public class InMemoryPartnerRepository : IPartnerRepository
{
    private readonly List<Partner> _partners =
    [
        new()
        {
            Pkid = 1, Name = "Microsoft", AppKey = "MS",
            NameOnPartnerMenu = "Microsoft 微軟", NameOnCourseDetailPage = "微軟",
            DisplayOrder = 1, ImageFilename = "microsoft.png",
            CourseCount = 12, CertificationCount = 3
        },
        new()
        {
            Pkid = 2, Name = "Cisco", AppKey = "CSCO",
            NameOnPartnerMenu = "Cisco 思科", NameOnCourseDetailPage = "思科",
            DisplayOrder = 2, ImageFilename = null,
            SeminarCount = 4
        },
        new()
        {
            Pkid = 3, Name = "Oracle", AppKey = "ORCL",
            NameOnPartnerMenu = "Oracle 甲骨文", NameOnCourseDetailPage = "甲骨文",
            DisplayOrder = 3, ImageFilename = null
        }
    ];

    /// <summary>Stands in for the IDENTITY counter: the next pkid SQL Server would hand out.</summary>
    private short _nextPkid = 4;

    public Task<IEnumerable<Partner>> GetAllAsync()
        => Task.FromResult<IEnumerable<Partner>>(Sorted(_partners).ToList());

    public Task<IEnumerable<Partner>> QueryAsync(PartnerQuery query)
    {
        IEnumerable<Partner> result = _partners;

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            result = result.Where(p =>
                p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || p.AppKey.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || p.NameOnPartnerMenu.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || p.NameOnCourseDetailPage.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        if (query.DisplayOrderFrom.HasValue)
        {
            result = result.Where(p => p.DisplayOrder >= query.DisplayOrderFrom.Value);
        }

        if (query.DisplayOrderTo.HasValue)
        {
            result = result.Where(p => p.DisplayOrder <= query.DisplayOrderTo.Value);
        }

        if (query.HasImage.HasValue)
        {
            result = query.HasImage.Value
                ? result.Where(p => p.ImageFilename is not null)
                : result.Where(p => p.ImageFilename is null);
        }

        return Task.FromResult<IEnumerable<Partner>>(Sorted(result).ToList());
    }

    public Task<Partner?> GetByPkidAsync(short pkid)
    {
        var partner = _partners.SingleOrDefault(p => p.Pkid == pkid);
        return Task.FromResult(partner is null ? null : Clone(partner));
    }

    public Task<short> CreateAsync(PartnerRequest request)
    {
        // The caller's pkid is ignored — the "database" assigns it.
        var pkid = _nextPkid++;

        _partners.Add(new Partner
        {
            Pkid = pkid,
            Name = request.Name,
            AppKey = request.AppKey,
            NameOnPartnerMenu = request.NameOnPartnerMenu,
            NameOnCourseDetailPage = request.NameOnCourseDetailPage,
            DisplayOrder = request.DisplayOrder,
            ImageFilename = request.ImageFilename
        });

        return Task.FromResult(pkid);
    }

    public Task<bool> UpdateAsync(PartnerRequest request)
    {
        var partner = _partners.SingleOrDefault(p => p.Pkid == request.Pkid);
        if (partner is null)
        {
            return Task.FromResult(false);
        }

        // pkid identifies the row — not updatable. Every other column is writable, AppKey included.
        partner.Name = request.Name;
        partner.AppKey = request.AppKey;
        partner.NameOnPartnerMenu = request.NameOnPartnerMenu;
        partner.NameOnCourseDetailPage = request.NameOnCourseDetailPage;
        partner.DisplayOrder = request.DisplayOrder;
        partner.ImageFilename = request.ImageFilename;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(short pkid)
    {
        var partner = _partners.SingleOrDefault(p => p.Pkid == pkid);
        return Task.FromResult(partner is not null && _partners.Remove(partner));
    }

    private static IEnumerable<Partner> Sorted(IEnumerable<Partner> partners)
        => partners.OrderBy(p => p.DisplayOrder).ThenBy(p => p.Pkid).Select(Clone);

    private static Partner Clone(Partner p) => new()
    {
        Pkid = p.Pkid,
        Name = p.Name,
        AppKey = p.AppKey,
        NameOnPartnerMenu = p.NameOnPartnerMenu,
        NameOnCourseDetailPage = p.NameOnCourseDetailPage,
        DisplayOrder = p.DisplayOrder,
        ImageFilename = p.ImageFilename,
        CourseCount = p.CourseCount,
        CertificationCount = p.CertificationCount,
        CourseGroupCount = p.CourseGroupCount,
        PromotionCount = p.PromotionCount,
        SeminarCount = p.SeminarCount
    };
}
