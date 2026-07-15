using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/featured-promo-items")]
[Produces("application/json")]
public class FeaturedPromoItemsController(IFeaturedPromoItemRepository repository) : ControllerBase
{
    /// <summary>取得所有上稿項目。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeaturedPromoItem>>> GetAll()
        => Ok(await repository.GetAllAsync());

    /// <summary>依查詢條件篩選上稿項目 (訓練中心分頁 + 一週日期範圍 + 欄位 + 關鍵字)。</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<FeaturedPromoItem>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<FeaturedPromoItem>>> Query([FromBody] FeaturedPromoItemQuery query)
        => Ok(await repository.QueryAsync(query ?? new FeaturedPromoItemQuery()));

    /// <summary>依主代碼取得單一上稿項目。</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeaturedPromoItem>> GetByPkid(int id)
    {
        var item = await repository.GetByPkidAsync(id);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>新增上稿項目 (主代碼由資料庫產生；同日期／訓練中心／欄位已排程時回傳 409)。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FeaturedPromoItem), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FeaturedPromoItem>> Create([FromBody] FeaturedPromoItemRequest request)
    {
        if (await repository.SlotTakenAsync(request.ScheduleOn, request.TrainingCenterPkid, request.Slot, excludePkid: 0))
        {
            return SlotConflict(request);
        }

        var pkid = await repository.CreateAsync(request);
        var created = await repository.GetByPkidAsync(pkid);
        return CreatedAtAction(nameof(GetByPkid), new { id = pkid }, created);
    }

    /// <summary>修改上稿項目 (主代碼由 body 傳入，且不可修改；欄位衝突時回傳 409)。</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromBody] FeaturedPromoItemRequest request)
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

        if (await repository.SlotTakenAsync(request.ScheduleOn, request.TrainingCenterPkid, request.Slot, excludePkid: request.Pkid))
        {
            return SlotConflict(request);
        }

        await repository.UpdateAsync(request);
        return NoContent();
    }

    /// <summary>刪除上稿項目。無任何子資料表參照，故一律可刪。</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await repository.GetByPkidAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        await repository.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Slot 往上／往下移動 (對應 UI 的「+」/「-」)。若目標欄位已有項目則兩者互換。已達邊界 (Slot 1 再往上、
    /// Slot 3 再往下) 回傳 409。
    /// </summary>
    [HttpPost("{id:int}/move")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Move(int id, [FromBody] FeaturedPromoItemMoveRequest request)
    {
        var result = await repository.MoveAsync(id, down: request.Direction == "down");
        return result switch
        {
            MoveResult.NotFound => NotFound(),
            MoveResult.OutOfRange => Conflict(new { message = "已達邊界，無法再移動。" }),
            _ => NoContent()
        };
    }

    private ConflictObjectResult SlotConflict(FeaturedPromoItemRequest request) => Conflict(new
    {
        message = $"{request.ScheduleOn:yyyy-MM-dd} 訓練中心第 {request.Slot} 欄位已有排程，請改用其他欄位。"
    });
}
