using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PackageVersionUpdater
{
    class Program
    {
        static Task Main(string[] args)
        {
            var command = new RootCommand
            {
                // Get name from process so that it will show correctly if run as a .NET CLI tool
                Name = GetProcessName(),
            };

            command.AddArgument(new Argument<FileInfo>("sln").ExistingOnly());
            command.AddOption(new Option<bool>("--verbose"));

            command.Handler = CommandHandler.Create<FileInfo, bool>(RunAsync);

            return command.InvokeAsync(args);

            static string GetProcessName()
            {
                using var current = System.Diagnostics.Process.GetCurrentProcess();
                return current.ProcessName;
            }

            static Task RunAsync(FileInfo sln, bool verbose)
                => Host.CreateDefaultBuilder()
                    .ConfigureServices((ctx, services) =>
                    {
                        services.AddHostedService<MainHost>();
                        services.AddPackageReferenceUpdater(options =>
                        {
                            options.Path = sln.FullName;
                        });
                    })
                    .ConfigureLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.SetMinimumLevel(verbose ? LogLevel.Trace : LogLevel.Information);
                    })
                    .RunConsoleAsync(options =>
                    {
                        options.SuppressStatusMessages = true;
                    });
        }

        private class MainHost : BackgroundService
        {
            private readonly IServiceProvider _services;
            private readonly ILogger<MainHost> _logger;
            private readonly IHostApplicationLifetime _lifetime;

            public MainHost(
                ILogger<MainHost> logger,
                IServiceProvider services,
                IHostApplicationLifetime lifetime)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
}
