using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/course-groups")]
[Produces("application/json")]
public class CourseGroupsController(ICourseGroupRepository repository) : ControllerBase
{
    /// <summary>取得所有課程群組。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CourseGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CourseGroup>>> GetAll()
        => Ok(await repository.GetAllAsync());

    /// <summary>依查詢條件篩選課程群組。</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<CourseGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CourseGroup>>> Query([FromBody] CourseGroupQuery query)
        => Ok(await repository.QueryAsync(query ?? new CourseGroupQuery()));

    /// <summary>依主代碼取得單一課程群組。</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseGroup>> GetByPkid(int id)
    {
        if (!TryToPkid(id, out var pkid))
        {
            return NotFound();
        }

        var group = await repository.GetByPkidAsync(pkid);
        return group is null ? NotFound() : Ok(group);
    }

    /// <summary>新增課程群組 (主代碼由資料庫產生)。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CourseGroup), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CourseGroup>> Create([FromBody] CourseGroupRequest request)
    {
        // No duplicate-key check: pkid is IDENTITY, and the schema puts no UNIQUE index on Description.
        var pkid = await repository.CreateAsync(request);
        var created = await repository.GetByPkidAsync(pkid);
        return CreatedAtAction(nameof(GetByPkid), new { id = (int)pkid }, created);
    }

    /// <summary>修改課程群組 (主代碼由 body 傳入，且不可修改)。</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] CourseGroupRequest request)
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
    /// 刪除課程群組 (仍有課程或夥伴課程群組關聯時回傳 409)。
    ///
    /// This guard is not the usual cosmetic upgrade over a raw FK violation. FK_Course_CourseGroup is
    /// declared ON DELETE CASCADE, so SQL Server would happily accept the DELETE and destroy every Course
    /// in the group along with it — the check below is the only thing that prevents that.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        if (!TryToPkid(id, out var pkid))
        {
            return NotFound();
        }

        var existing = await repository.GetByPkidAsync(pkid);
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
                message = $"課程群組「{existing.Description}」仍有 {string.Join("、", blockers)}，無法刪除。"
            });
        }

        await repository.DeleteAsync(pkid);
        return NoContent();
    }

    /// <summary>Names only the child tables that actually have rows, so the 409 message stays readable.</summary>
    private static List<string> DescribeBlockers(CourseGroup group)
    {
        var blockers = new List<string>();

        if (group.CourseCount > 0) blockers.Add($"{group.CourseCount} 筆課程");
        if (group.PartnerCourseGroupCount > 0) blockers.Add($"{group.PartnerCourseGroupCount} 筆夥伴課程群組");

        return blockers;
    }

    /// <summary>The route takes an int (there is no :short constraint); anything outside smallint cannot exist.</summary>
    private static bool TryToPkid(int id, out short pkid)
    {
        if (id is >= short.MinValue and <= short.MaxValue)
        {
            pkid = (short)id;
            return true;
        }

        pkid = 0;
        return false;
    }
}
