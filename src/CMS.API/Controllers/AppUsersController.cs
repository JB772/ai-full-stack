using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/app-users")]
[Produces("application/json")]
public class AppUsersController(IAppUserRepository repository) : ControllerBase
{
    /// <summary>取得所有使用者。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppUser>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppUser>>> GetAll()
        => Ok(await repository.GetAllAsync());

    /// <summary>依查詢條件篩選使用者。</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<AppUser>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppUser>>> Query([FromBody] AppUserQuery query)
        => Ok(await repository.QueryAsync(query ?? new AppUserQuery()));

    /// <summary>依主代碼取得單一使用者。</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppUser>> GetByPkid(int id)
    {
        var user = await repository.GetByPkidAsync(id);
        return user is null ? NotFound() : Ok(user);
    }

    /// <summary>新增使用者 (密碼取自系統預設值)。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppUser), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppUser>> Create([FromBody] AppUserRequest request)
    {
        if (await repository.UserIdExistsAsync(request.UserId))
        {
            return Conflict(new { message = $"帳號「{request.UserId}」已存在。" });
        }

        var pkid = await repository.CreateAsync(request);
        var created = await repository.GetByPkidAsync(pkid);
        return CreatedAtAction(nameof(GetByPkid), new { id = pkid }, created);
    }

    /// <summary>修改使用者 (主代碼由 body 傳入；帳號與密碼不可修改)。</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] AppUserRequest request)
    {
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

    /// <summary>刪除使用者 (仍有角色關聯時回傳 409)。</summary>
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

        var roleCount = await repository.GetRoleCountAsync(id);
        if (roleCount > 0)
        {
            return Conflict(new { message = $"使用者「{existing.UserId}」仍有 {roleCount} 個角色關聯，無法刪除。" });
        }

        await repository.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>將使用者密碼重設為系統預設值 (不需傳入任何密碼)。</summary>
    [HttpPost("{id:int}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var reset = await repository.ResetPasswordAsync(id);
        return reset ? NoContent() : NotFound();
    }
}
