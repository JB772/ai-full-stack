using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/courses")]
[Produces("application/json")]
public class CoursesController(ICourseRepository repository) : ControllerBase
{
    /// <summary>取得所有課程。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Course>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Course>>> GetAll()
        => Ok(await repository.GetAllAsync());

    /// <summary>依查詢條件篩選課程。</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<Course>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Course>>> Query([FromBody] CourseQuery query)
        => Ok(await repository.QueryAsync(query ?? new CourseQuery()));

    /// <summary>依主代碼取得單一課程。</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Course), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Course>> GetByPkid(int id)
    {
        var course = await repository.GetByPkidAsync(id);
        return course is null ? NotFound() : Ok(course);
    }

    /// <summary>新增課程 (主代碼由資料庫產生)。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Course), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Course>> Create([FromBody] CourseRequest request)
    {
        // pkid is IDENTITY; there is no natural-key UNIQUE constraint on Course, so no duplicate 409.
        var pkid = await repository.CreateAsync(request);
        var created = await repository.GetByPkidAsync(pkid);
        return CreatedAtAction(nameof(GetByPkid), new { id = pkid }, created);
    }

    /// <summary>修改課程 (主代碼由 body 傳入，且不可修改)。</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] CourseRequest request)
    {
        // pkid is an IDENTITY seeded at 1, so 0 or negative means "absent" — a 400, not a 404.
        if (request.Pkid <= 0)
        {
            return BadRequest(new { message = "主代碼為必填。" });
        }

        var existing = await repository.GetByPkidAsync(request.Pkid);
        if (existing is null)
        {
            return NotFound();
        }

        await repository.UpdateAsync(request);
        return NoContent();
    }

    /// <summary>
    /// 刪除課程 (仍有問答、認證、職類、相關連結、熱門課程或推薦課程關聯時回傳 409)。
    ///
    /// CourseInCertification / CourseJobCategories are ON DELETE CASCADE, so this guard is protective,
    /// not cosmetic: without it SQL Server would accept the delete and destroy those junction rows.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await repository.GetByPkidAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        // The counts are already on the entity loaded above — no second round trip.
        var blockers = DescribeBlockers(existing);
        if (blockers.Count > 0)
        {
            return Conflict(new
            {
                message = $"課程「{existing.Title}」仍有 {string.Join("、", blockers)}，無法刪除。"
            });
        }

        await repository.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>Names only the child tables that actually have rows, so the 409 message stays readable.</summary>
    private static List<string> DescribeBlockers(Course course)
    {
        var blockers = new List<string>();

        if (course.CourseFaqCount > 0) blockers.Add($"{course.CourseFaqCount} 筆問答");
        if (course.CertificationCount > 0) blockers.Add($"{course.CertificationCount} 筆認證關聯");
        if (course.JobCategoryCount > 0) blockers.Add($"{course.JobCategoryCount} 筆職類關聯");
        if (course.RelatedLinkCount > 0) blockers.Add($"{course.RelatedLinkCount} 筆相關連結");
        if (course.HotCourseCount > 0) blockers.Add($"{course.HotCourseCount} 筆熱門課程");
        if (course.RecommCount > 0) blockers.Add($"{course.RecommCount} 筆推薦課程");

        return blockers;
    }
}
