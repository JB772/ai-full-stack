using CMS.API.Models;
using CMS.API.Repositories;
using CMS.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CMS.API.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
[AllowAnonymous] // login must be reachable without a token; every other controller requires auth (see Program.cs FallbackPolicy)
public class AuthController(IAuthRepository repository, IJwtTokenService tokenService) : ControllerBase
{
    /// <summary>登入。驗證帳密後回傳含 JWT 的使用者資訊；任何一項不符皆回傳通用 401。</summary>
    [HttpPost("login")]
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
}
