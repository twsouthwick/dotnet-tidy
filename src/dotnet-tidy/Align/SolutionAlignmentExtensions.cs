using Microsoft.Extensions.DependencyInjection;
using System;

namespace PackageVersionUpdater
{
    public static class SolutionAlignmentExtensions
    {
        public static void AddSolutionAlignment(this IServiceCollection services, Action<AlignOptions> configure)
        {
            services.AddTransient<IApplication, AlignSolutionApp>();
            services.AddOptions<AlignOptions>()
                .Configure(configure);
        }
    }
}
