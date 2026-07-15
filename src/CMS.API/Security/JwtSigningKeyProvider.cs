using CMS.API.Repositories;

namespace CMS.API.Security;

/// <summary>
/// 供 JWT bearer 驗證使用的簽章金鑰來源。金鑰與 <see cref="JwtTokenService"/> 簽發時所用者相同 ——
/// 皆取自 SysConfig['appConfig'].symmetricSecurityKey (經 <see cref="IAuthRepository"/> 讀取)。
/// 首次使用時載入並快取；validation 期間以同步方式取得，故用 scope 解析 scoped 的 repository。
/// </summary>
public class JwtSigningKeyProvider(IServiceScopeFactory scopeFactory)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _cachedKey;

    public string Get()
    {
        if (_cachedKey is not null)
        {
            return _cachedKey;
        }

        _gate.Wait();
        try
        {
            if (_cachedKey is null)
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
                _cachedKey = repository.GetSigningKeyAsync().GetAwaiter().GetResult();
            }

            return _cachedKey;
        }
        finally
        {
            _gate.Release();
        }
    }
}
