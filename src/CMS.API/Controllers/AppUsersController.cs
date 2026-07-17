using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

/// <summary>
/// 使用者 (AppUser) 維護 —— 全控制器僅限 Admin (含帳號的建立/修改/刪除與角色指派)。
///
/// 屬於 app.ts「系統管理 Admin」導覽群組。在加上這個類別層級屬性之前，只有角色指派/移除兩個動作被擋，
/// 帳號的 Create/Update/Delete 則否 —— 而 AppUserRequest 帶有 IsActive、登入又要求 IsActive = 1，
/// 等於任何登入者都能停用任何管理員帳號。詳見 docs/auth.md。
/// </summary>
[ApiController]
[Route("api/app-users")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
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

    /// <summary>取得使用者目前的角色清單 (含角色名稱)。</summary>
    [HttpGet("{id:int}/roles")]
    [ProducesResponseType(typeof(IEnumerable<UserRole>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<UserRole>>> GetRoles(int id)
    {
        var user = await repository.GetByPkidAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(await repository.GetRolesAsync(id));
    }

    /// <summary>指派角色給使用者 (僅限 Admin —— 由類別層級的 [Authorize] 涵蓋)。回傳更新後的角色清單。</summary>
    [HttpPost("{id:int}/roles")]
    [ProducesResponseType(typeof(IEnumerable<UserRole>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IEnumerable<UserRole>>> AssignRole(int id, [FromBody] AssignRoleRequest request)
    {
        var roleId = request?.RoleId?.Trim();
        if (string.IsNullOrEmpty(roleId))
        {
            return BadRequest(new { message = "角色代碼為必填。" });
        }

        var user = await repository.GetByPkidAsync(id);
        if (user is null)
        {
            return NotFound(new { message = "找不到該使用者。" });
        }

        if (!await repository.RoleExistsAsync(roleId))
        {
            return NotFound(new { message = $"找不到角色「{roleId}」。" });
        }

        if (await repository.HasRoleAsync(id, roleId))
        {
            return Conflict(new { message = $"使用者「{user.UserId}」已擁有角色「{roleId}」。" });
        }

        await repository.AssignRoleAsync(id, roleId);
        var roles = await repository.GetRolesAsync(id);
        return CreatedAtAction(nameof(GetRoles), new { id }, roles);
    }

    /// <summary>移除使用者的角色 (僅限 Admin —— 由類別層級的 [Authorize] 涵蓋)。</summary>
    [HttpDelete("{id:int}/roles/{roleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRole(int id, string roleId)
    {
        var user = await repository.GetByPkidAsync(id);
        if (user is null)
        {
            return NotFound(new { message = "找不到該使用者。" });
        }

        var removed = await repository.RemoveRoleAsync(id, roleId);
        return removed ? NoContent() : NotFound(new { message = $"使用者「{user.UserId}」未擁有角色「{roleId}」。" });
    }
}
