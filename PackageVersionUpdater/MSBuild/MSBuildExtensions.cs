using Microsoft.Extensions.DependencyInjection;

namespace PackageVersionUpdater
{
    public static class MSBuildExtensions
    {
        public static void AddMSBuild(this IServiceCollection services)
        {
            services.AddSingleton<Registrar>();
        }
    }
}
