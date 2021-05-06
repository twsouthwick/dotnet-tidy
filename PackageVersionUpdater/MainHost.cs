using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PackageVersionUpdater
{
    internal class MainHost : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly IHostApplicationLifetime _lifetime;

        public MainHost(
            IServiceProvider services,
            IHostApplicationLifetime lifetime)
        {
            _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _services.CreateScope();

                var app = scope.ServiceProvider.GetRequiredService<IApplication>();

                await app.RunAsync(stoppingToken).ConfigureAwait(false);
            }
            finally
            {
                _lifetime.StopApplication();
            }
        }
    }
}
