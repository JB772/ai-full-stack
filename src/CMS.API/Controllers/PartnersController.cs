using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/partners")]
[Produces("application/json")]
public class PartnersController(IPartnerRepository repository) : ControllerBase
{
    /// <summary>取得所有合作夥伴。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Partner>>> GetAll()
        => Ok(await repository.GetAllAsync());

    /// <summary>依查詢條件篩選合作夥伴。</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Partner>>> Query([FromBody] PartnerQuery query)
        => Ok(await repository.QueryAsync(query ?? new PartnerQuery()));

    /// <summary>依主代碼取得單一合作夥伴。</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Partner>> GetByPkid(int id)
    {
        if (!TryToPkid(id, out var pkid))
        {
            return NotFound();
        }

        var partner = await repository.GetByPkidAsync(pkid);
        return partner is null ? NotFound() : Ok(partner);
    }

    /// <summary>新增合作夥伴 (主代碼由資料庫產生)。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Partner), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Partner>> Create([FromBody] PartnerRequest request)
    {
        // No duplicate-key check: pkid is IDENTITY, and the schema puts no UNIQUE index on AppKey.
        var pkid = await repository.CreateAsync(request);
        var created = await repository.GetByPkidAsync(pkid);
        return CreatedAtAction(nameof(GetByPkid), new { id = (int)pkid }, created);
    }

    /// <summary>修改合作夥伴 (主代碼由 body 傳入，且不可修改)。</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] PartnerRequest request)
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

    /// <summary>刪除合作夥伴 (仍有課程、認證、課程群組、促銷活動或研討會關聯時回傳 409)。</summary>
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
                message = $"合作夥伴「{existing.Name}」仍有 {string.Join("、", blockers)}，無法刪除。"
            });
        }

        await repository.DeleteAsync(pkid);
        return NoContent();
    }

    /// <summary>
    /// Names only the child tables that actually have rows, so the 409 message stays readable.
    /// Seminar is included even though the schema declares no FK for it — deleting the partner would
    /// still orphan those rows.
    /// </summary>
    private static List<string> DescribeBlockers(Partner partner)
    {
        var blockers = new List<string>();

        if (partner.CourseCount > 0) blockers.Add($"{partner.CourseCount} 筆課程");
        if (partner.CertificationCount > 0) blockers.Add($"{partner.CertificationCount} 筆認證");
        if (partner.CourseGroupCount > 0) blockers.Add($"{partner.CourseGroupCount} 筆課程群組");
        if (partner.PromotionCount > 0) blockers.Add($"{partner.PromotionCount} 筆促銷活動");
        if (partner.SeminarCount > 0) blockers.Add($"{partner.SeminarCount} 筆研討會");

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
