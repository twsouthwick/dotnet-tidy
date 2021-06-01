using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
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

            var alignCommand = new Command("align");
            alignCommand.AddArgument(new Argument<FileInfo>("sln").ExistingOnly());
            alignCommand.AddOption(new Option<bool>("--verbose"));
            alignCommand.Handler = CommandHandler.Create<FileInfo, bool>(RunMoveAsync);

            var enableCentralManagementCommand = new Command("enable");
            enableCentralManagementCommand.AddArgument(new Argument<FileInfo>("sln").ExistingOnly());
            enableCentralManagementCommand.AddOption(new Option<bool>("--verbose"));
            enableCentralManagementCommand.Handler = CommandHandler.Create<FileInfo, bool>(RunNuGetAsync);

            var removeCentralManagementCommand = new Command("disable");
            removeCentralManagementCommand.AddArgument(new Argument<FileInfo>("sln").ExistingOnly());
            removeCentralManagementCommand.AddOption(new Option<bool>("--verbose"));
            removeCentralManagementCommand.Handler = CommandHandler.Create<FileInfo, bool>(RemoveCentral);

            var centralManagementCommand = new Command("centrally-managed")
            {
                enableCentralManagementCommand,
                removeCentralManagementCommand
            };

            var packageManagementCommand = new Command("packages")
            {
                centralManagementCommand
            };

            var solutionCommand = new Command("sln")
            {
                alignCommand
            };

            command.Add(solutionCommand);
            command.Add(packageManagementCommand);

            return command.InvokeAsync(args);

            static string GetProcessName()
            {
                using var current = System.Diagnostics.Process.GetCurrentProcess();
                return current.ProcessName;
            }

            static Task RunMoveAsync(FileInfo sln, bool verbose)
                => RunAsync(services =>
                {
                    services.AddMSBuild();
                    services.AddSolutionAlignment(options =>
                    {
                        options.SolutionPath = sln.FullName;
                    });
                }, verbose);

            static Task RemoveCentral(FileInfo sln, bool verbose)
                => RunAsync(services =>
                {
                    services.AddMSBuild();
                    services.AddPackageReferenceUpdater(false, options =>
                    {
                        options.Path = sln.FullName;
                    });
                }, verbose);

            static Task RunNuGetAsync(FileInfo sln, bool verbose)
                => RunAsync(services =>
                {
                    services.AddMSBuild();
                    services.AddPackageReferenceUpdater(true, options =>
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
    }
}
