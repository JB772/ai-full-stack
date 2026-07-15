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

    /// <summary>
    /// 變更登入者本人的密碼。身分 (UserId) 一律取自 JWT，永不採用請求本文。流程：
    /// (1) 先驗證目前密碼 (SHA256 須與資料庫 PasswordHash 相符)，不符則不做任何變更；
    /// (2) 檢查新密碼複雜度；(3) 新密碼與確認密碼須相符；(4) 成功後寫入新雜湊並更新 PasswordUpdatedTime。
    /// 明文密碼僅用於驗證與雜湊，密碼雜湊永不對外回傳。此端點需有效 Bearer 權杖。
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        // 身分永遠取自權杖 (NameClaimType = userId)，忽略任何本文中的 UserId。
        var userId = User.FindFirstValue(JwtTokenService.UserIdClaimType);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var currentPassword = request?.CurrentPassword ?? string.Empty;
        var newPassword = request?.NewPassword ?? string.Empty;
        var confirmNewPassword = request?.ConfirmNewPassword ?? string.Empty;

        // (1) 驗證目前密碼：重用登入驗證 (UserId 相符 + IsActive + PasswordHash 相符)，全在 SQL WHERE 內比對。
        var currentHash = PasswordHasher.Hash(currentPassword);
        var user = await repository.AuthenticateAsync(userId, currentHash);
        if (user is null)
        {
            return BadRequest(new { message = "目前密碼不正確 Current password is incorrect." });
        }

        // (2) 新密碼複雜度。
        if (!PasswordPolicy.IsComplexEnough(newPassword))
        {
            return BadRequest(new { message = PasswordPolicy.ComplexityMessage });
        }

        // (3) 新密碼與確認密碼須相符。
        if (newPassword != confirmNewPassword)
        {
            return BadRequest(new { message = "新密碼與確認密碼不一致 New password and confirmation do not match." });
        }

        // (4) 成功：只寫入新雜湊 (含 PasswordUpdatedTime = now)，永不回傳任何雜湊。
        var newHash = PasswordHasher.Hash(newPassword);
        await repository.UpdatePasswordAsync(userId, newHash);

        return NoContent();
    }
}
