using Microsoft.Extensions.DependencyInjection;
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

            var moveCommand = new Command("mv");
            moveCommand.AddArgument(new Argument<FileInfo>("sln").ExistingOnly());
            moveCommand.AddArgument(new Argument<FileInfo>("project").ExistingOnly());
            moveCommand.AddArgument(new Argument<DirectoryInfo>("destination"));
            moveCommand.AddOption(new Option<bool>("--verbose"));
            moveCommand.Handler = CommandHandler.Create<FileInfo, FileInfo, DirectoryInfo, bool>(RunMoveAsync);

            var nuGetCommand = new Command("nuget");
            nuGetCommand.AddArgument(new Argument<FileInfo>("sln").ExistingOnly());
            nuGetCommand.AddOption(new Option<bool>("--verbose"));
            nuGetCommand.Handler = CommandHandler.Create<FileInfo, bool>(RunNuGetAsync);

            command.Add(moveCommand);
            command.Add(nuGetCommand);

            return command.InvokeAsync(args);

            static string GetProcessName()
            {
                using var current = System.Diagnostics.Process.GetCurrentProcess();
                return current.ProcessName;
            }

            static Task RunMoveAsync(FileInfo sln, FileInfo project, DirectoryInfo destination, bool verbose)
                => RunAsync(services =>
                {
                    services.AddMSBuild();
                    services.AddMoveHelpers(options =>
                    {
                        options.DestinationDirectory = destination.FullName;
                        options.ProjectPath = project.FullName;
                        options.SolutionPath = sln.FullName;
                    });
                }, verbose);

            static Task RunNuGetAsync(FileInfo sln, bool verbose)
                => RunAsync(services =>
                {
                    services.AddMSBuild();
                    services.AddPackageReferenceUpdater(options =>
                    {
                        options.Path = sln.FullName;
                    });
                }, verbose);

            static Task RunAsync(Action<IServiceCollection> serviceAction, bool verbose)
                => Host.CreateDefaultBuilder()
                    .ConfigureServices((ctx, services) =>
                    {
                        services.AddHostedService<MainHost>();
                        serviceAction(services);
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
