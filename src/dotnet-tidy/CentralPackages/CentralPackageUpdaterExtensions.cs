using Microsoft.Extensions.DependencyInjection;
using System;

namespace PackageVersionUpdater
{
    public static class CentralPackageUpdaterExtensions
    {
        public static void AddPackageReferenceUpdater(this IServiceCollection services, Action<UpdaterOptions> configure)
        {
            services.AddOptions<UpdaterOptions>()
                .Configure(configure);

            services.AddTransient<IApplication, CentralPackageUpdater>();
        }
    }
}
