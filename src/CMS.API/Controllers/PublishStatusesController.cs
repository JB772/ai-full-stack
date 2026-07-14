using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/publish-statuses")]
[Produces("application/json")]
public class PublishStatusesController(IPublishStatusRepository repository) : ControllerBase
{
    /// <summary>取得所有發布狀態。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> GetAll()
        => Ok(await repository.GetAllAsync());

    /// <summary>依查詢條件篩選發布狀態。</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> Query([FromBody] PublishStatusQuery query)
        => Ok(await repository.QueryAsync(query ?? new PublishStatusQuery()));

    /// <summary>依主代碼取得單一發布狀態。</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishStatus>> GetByPkid(int id)
    {
        if (!TryToPkid(id, out var pkid))
        {
            return NotFound();
        }

        var status = await repository.GetByPkidAsync(pkid);
        return status is null ? NotFound() : Ok(status);
    }

    /// <summary>新增發布狀態 (主代碼由使用者指定，重複時回傳 409)。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PublishStatus), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PublishStatus>> Create([FromBody] PublishStatusRequest request)
    {
        // pkid is not an IDENTITY column — a duplicate would raise a raw PK violation from SQL Server.
        if (await repository.PkidExistsAsync(request.Pkid))
        {
            return Conflict(new { message = $"主代碼「{request.Pkid}」已存在。" });
        }

        var pkid = await repository.CreateAsync(request);
        var created = await repository.GetByPkidAsync(pkid);
        return CreatedAtAction(nameof(GetByPkid), new { id = (int)pkid }, created);
    }

    /// <summary>修改發布狀態 (主代碼由 body 傳入，且不可修改)。</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] PublishStatusRequest request)
    {
        // No "pkid must be > 0" guard here (unlike AppRole): 0 is a legal tinyint key,
        // so an unknown pkid is a 404, not a 400.
        var existing = await repository.GetByPkidAsync(request.Pkid);
        if (existing is null)
        {
            return NotFound();
        }

        await repository.UpdateAsync(request);
        return NoContent();
    }

    /// <summary>刪除發布狀態 (仍被課程或促銷活動引用時回傳 409)。</summary>
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

        if (existing.CourseCount > 0 || existing.PromotionCount > 0)
        {
            return Conflict(new
            {
                message = $"發布狀態「{existing.Description}」仍有 {existing.CourseCount} 筆課程、"
                          + $"{existing.PromotionCount} 筆促銷活動，無法刪除。"
            });
        }

        await repository.DeleteAsync(pkid);
        return NoContent();
    }

    /// <summary>The route takes an int (there is no :byte constraint); anything outside tinyint cannot exist.</summary>
    private static bool TryToPkid(int id, out byte pkid)
    {
        if (id is >= byte.MinValue and <= byte.MaxValue)
        {
            pkid = (byte)id;
            return true;
        }

        pkid = 0;
        return false;
    }
}
