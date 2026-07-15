using System.Security.Claims;
using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IAuthRepository repository, IJwtTokenService tokenService) : ControllerBase
{
    /// <summary>登入。驗證帳密後回傳含 JWT 的使用者資訊；任何一項不符皆回傳通用 401。</summary>
    [HttpPost("login")]
    [AllowAnonymous] // login must be reachable without a token; every other endpoint requires auth (see Program.cs FallbackPolicy)
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        // 通用 401：不揭露是帳號、密碼、還是啟用狀態出錯。
        if (request is null || string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Password))
        {
            return Unauthorized(new { message = "invalid credentials" });
        }

        var passwordHash = PasswordHasher.Hash(request.Password);
        var user = await repository.AuthenticateAsync(request.UserId, passwordHash);
        if (user is null)
        {
            return Unauthorized(new { message = "invalid credentials" });
        }

        var signingKey = await repository.GetSigningKeyAsync();
        var accessToken = tokenService.CreateToken(user.UserId, user.UserName, user.RoleIds, signingKey);

        return Ok(new LoginResponse
        {
            UserId = user.UserId,
            UserName = user.UserName,
            AccessToken = accessToken
        });
    }

    /// <summary>
    /// 更新登入者本人的 UserName。身分 (UserId) 一律取自 JWT，永不採用請求本文的 UserId；
    /// 角色亦無法經此端點變更。此端點需有效 Bearer 權杖 (不加 [AllowAnonymous]，套用全域授權)。
    /// </summary>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        // 身分永遠取自權杖 (NameClaimType = userId)，忽略 request.UserId。
        var userId = User.FindFirstValue(JwtTokenService.UserIdClaimType);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var userName = request?.UserName?.Trim();
        if (string.IsNullOrEmpty(userName))
        {
            return BadRequest(new { message = "UserName is required" });
        }

        var updated = await repository.UpdateUserNameAsync(userId, userName);
        if (updated is null)
        {
            return NotFound(new { message = "user not found" });
        }

        return Ok(new ProfileResponse
        {
            UserId = updated.UserId,
            UserName = updated.UserName,
            Roles = updated.RoleIds
        });
    }
}
