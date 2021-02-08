using Microsoft.Extensions.DependencyInjection;
using System;

namespace PackageVersionUpdater
{
    public static class PackageUpdaterExtensions
    {
        public static void AddPackageReferenceUpdater(this IServiceCollection services, Action<UpdaterOptions> configure)
        {
            services.AddOptions<UpdaterOptions>()
                .Configure(configure);

            services.AddSingleton(new Registrar());
            services.AddTransient<IApplication, PackageUpdater>();
        }
    }
}
