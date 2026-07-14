using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/app-roles")]
[Produces("application/json")]
public class AppRolesController(IAppRoleRepository repository) : ControllerBase
{
    /// <summary>取得所有角色。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRole>>> GetAll()
        => Ok(await repository.GetAllAsync());

    /// <summary>依查詢條件篩選角色。</summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(IEnumerable<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRole>>> Query([FromBody] AppRoleQuery query)
        => Ok(await repository.QueryAsync(query ?? new AppRoleQuery()));

    /// <summary>依主代碼取得單一角色。</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppRole>> GetByPkid(int id)
    {
        var role = await repository.GetByPkidAsync(id);
        return role is null ? NotFound() : Ok(role);
    }

    /// <summary>新增角色。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AppRole), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AppRole>> Create([FromBody] AppRoleRequest request)
    {
        if (await repository.RoleIdExistsAsync(request.RoleId))
        {
            return Conflict(new { message = $"角色代碼「{request.RoleId}」已存在。" });
        }

        var pkid = await repository.CreateAsync(request);
        var created = await repository.GetByPkidAsync(pkid);
        return CreatedAtAction(nameof(GetByPkid), new { id = pkid }, created);
    }

    /// <summary>修改角色 (主代碼由 body 傳入；角色代碼不可修改)。</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] AppRoleRequest request)
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

    /// <summary>刪除角色 (仍有使用者關聯時回傳 409)。</summary>
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

        var userCount = await repository.GetUserCountAsync(id);
        if (userCount > 0)
        {
            return Conflict(new { message = $"角色「{existing.RoleId}」仍有 {userCount} 位使用者，無法刪除。" });
        }

        await repository.DeleteAsync(id);
        return NoContent();
    }
}
