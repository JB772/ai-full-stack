using CMS.API.Repositories;
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
            services.RemoveAll<IAppRoleRepository>();
            services.AddSingleton<IAppRoleRepository, InMemoryAppRoleRepository>();

            services.RemoveAll<IPublishStatusRepository>();
            services.AddSingleton<IPublishStatusRepository, InMemoryPublishStatusRepository>();

            services.RemoveAll<IPartnerRepository>();
            services.AddSingleton<IPartnerRepository, InMemoryPartnerRepository>();

            services.RemoveAll<ICourseGroupRepository>();
            services.AddSingleton<ICourseGroupRepository, InMemoryCourseGroupRepository>();

            services.RemoveAll<ICourseRepository>();
            services.AddSingleton<ICourseRepository, InMemoryCourseRepository>();
        });
    }
}
