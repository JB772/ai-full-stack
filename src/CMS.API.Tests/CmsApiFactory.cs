using System.Net.Http.Headers;
using CMS.API.Repositories;
using CMS.API.Security;
using CMS.API.Tests.Fakes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CMS.API.Tests;

/// <summary>Boots the real API pipeline (routing, model binding, controllers) with an in-memory repository.</summary>
public class CmsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAuthRepository>();
            services.AddSingleton<IAuthRepository, InMemoryAuthRepository>();

            services.RemoveAll<IAppRoleRepository>();
            services.AddSingleton<IAppRoleRepository, InMemoryAppRoleRepository>();

            services.RemoveAll<IAppUserRepository>();
            services.AddSingleton<IAppUserRepository, InMemoryAppUserRepository>();

            services.RemoveAll<IPublishStatusRepository>();
            services.AddSingleton<IPublishStatusRepository, InMemoryPublishStatusRepository>();

            services.RemoveAll<IPartnerRepository>();
            services.AddSingleton<IPartnerRepository, InMemoryPartnerRepository>();

            services.RemoveAll<ICourseGroupRepository>();
            services.AddSingleton<ICourseGroupRepository, InMemoryCourseGroupRepository>();

            services.RemoveAll<ICourseRepository>();
            services.AddSingleton<ICourseRepository, InMemoryCourseRepository>();

            services.RemoveAll<IFeaturedPromoItemRepository>();
            services.AddSingleton<IFeaturedPromoItemRepository, InMemoryFeaturedPromoItemRepository>();

            services.RemoveAll<ITrainingCenterRepository>();
            services.AddSingleton<ITrainingCenterRepository, InMemoryTrainingCenterRepository>();

            services.RemoveAll<IPromotionRepository>();
            services.AddSingleton<IPromotionRepository, InMemoryPromotionRepository>();
        });
    }

    /// <summary>
    /// A client whose every request carries a valid Bearer token — the default for exercising the
    /// now-protected feature endpoints. The token is signed with the same in-memory signing key the
    /// running app validates against, so it authenticates for real (no auth bypass).
    /// </summary>
    public HttpClient CreateAuthenticatedClient(string userId = "admin", string userName = "系統管理員", params string[] roles)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(userId, userName, roles));
        return client;
    }

    /// <summary>Issues a JWT via the real <see cref="IJwtTokenService"/> and in-memory signing key.</summary>
    public string CreateToken(string userId = "admin", string userName = "系統管理員", params string[] roles)
    {
        using var scope = Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var authRepository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var signingKey = authRepository.GetSigningKeyAsync().GetAwaiter().GetResult();
        var effectiveRoles = roles.Length > 0 ? roles : ["Admin"];
        return tokenService.CreateToken(userId, userName, effectiveRoles, signingKey);
    }
}
