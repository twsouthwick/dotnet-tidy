using Microsoft.Extensions.DependencyInjection;
using System;

namespace PackageVersionUpdater
{
    public static class CentralPackageUpdaterExtensions
    {
        public static void AddPackageReferenceUpdater(this IServiceCollection services, bool add, Action<UpdaterOptions> configure)
        {
            services.AddOptions<UpdaterOptions>()
                .Configure(configure);

            if (add)
            {
                services.AddTransient<IApplication, CentralPackageUpdater>();
            }
            else
            {
                services.AddTransient<IApplication, RemoveCentralPackageManagementUpdater>();
            }
        }
    }
}
