using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController(
    IAppRoleRepository appRoleRepository,
    IPublishStatusRepository publishStatusRepository) : ControllerBase
{
    /// <summary>角色下拉選單資料。</summary>
    [HttpGet("app-roles")]
    [ProducesResponseType(typeof(IEnumerable<AppRole>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<AppRole>>> GetAppRoles()
        => Ok(await appRoleRepository.GetAllAsync());

    /// <summary>發布狀態下拉選單資料 (依主代碼排序，顯示 Description)。</summary>
    [HttpGet("publish-statuses")]
    [ProducesResponseType(typeof(IEnumerable<PublishStatus>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PublishStatus>>> GetPublishStatuses()
        => Ok(await publishStatusRepository.GetAllAsync());
}
