using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CMS.API.Security;

public interface IJwtTokenService
{
    /// <summary>
    /// 以對稱金鑰 (HMAC-SHA256) 簽發存取權杖。權杖含 UserId、UserName 與每個 RoleId 的角色宣告，
    /// 有效期 24 小時。金鑰由呼叫端於執行期提供 (取自 SysConfig)，不寫死於此。
    /// </summary>
    string CreateToken(string userId, string userName, IEnumerable<string> roleIds, string signingKey);
}

/// <summary>JWT 簽發工具。純運算、無資料庫相依，方便單元測試。</summary>
public class JwtTokenService : IJwtTokenService
{
    public const string UserIdClaimType = "userId";
    public const string UserNameClaimType = "userName";
    public const string RoleClaimType = "role";

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public string CreateToken(string userId, string userName, IEnumerable<string> roleIds, string signingKey)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(UserIdClaimType, userId),
            new(UserNameClaimType, userName),
        };
        claims.AddRange(roleIds.Select(roleId => new Claim(RoleClaimType, roleId)));

        var issuedAt = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: issuedAt,
            expires: issuedAt.Add(TokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
