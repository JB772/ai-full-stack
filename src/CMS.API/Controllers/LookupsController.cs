using CMS.API.Models;
using CMS.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/lookups")]
[Produces("application/json")]
public class LookupsController(
    IAppRoleRepository appRoleRepository,
    IPublishStatusRepository publishStatusRepository,
    IPartnerRepository partnerRepository,
    ICourseGroupRepository courseGroupRepository,
    ITrainingCenterRepository trainingCenterRepository,
    IPromotionRepository promotionRepository) : ControllerBase
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

    /// <summary>合作夥伴下拉選單資料 (依顯示順序排序，顯示 Name)。</summary>
    [HttpGet("partners")]
    [ProducesResponseType(typeof(IEnumerable<Partner>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Partner>>> GetPartners()
        => Ok(await partnerRepository.GetAllAsync());

    /// <summary>課程群組下拉選單資料 (依群組說明排序，顯示 Description)。</summary>
    [HttpGet("course-groups")]
    [ProducesResponseType(typeof(IEnumerable<CourseGroup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CourseGroup>>> GetCourseGroups()
        => Ok(await courseGroupRepository.GetAllAsync());

    /// <summary>訓練中心下拉／分頁資料 (依 DisplayOrder 排序，顯示 Name)。</summary>
    [HttpGet("training-centers")]
    [ProducesResponseType(typeof(IEnumerable<TrainingCenterLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TrainingCenterLookup>>> GetTrainingCenters()
        => Ok(await trainingCenterRepository.GetAllAsync());

    /// <summary>促銷下拉／查詢資料 (依 PromoCode 排序)，供上稿表單以 PromoCode 解析 Promotion_pkid。</summary>
    [HttpGet("promo-codes")]
    [ProducesResponseType(typeof(IEnumerable<PromotionLookup>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PromotionLookup>>> GetPromoCodes()
        => Ok(await promotionRepository.GetAllAsync());
}
