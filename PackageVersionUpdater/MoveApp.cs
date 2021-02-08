using Microsoft.Build.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PackageVersionUpdater
{
    public static class MoveExtensions
    {
        public static void AddMoveHelpers(IServiceCollection services, Action<MoveOptions> configure)
        {
            services.AddTransient<IApplication, MoveApp>();
            services.AddOptions<MoveOptions>()
                .Configure(configure);
        }
    }

    public class MoveOptions
    {
        public string SolutionPath { get; set; }

        public string ProjectPath { get; set; }

        public string DestinationDirectory { get; set; }
    }

    public class MoveApp : IApplication
    {
        private readonly Registrar _registrar;
        private readonly MoveOptions _options;
        private readonly ILogger<MoveApp> _logger;

        public MoveApp(
            Registrar registrar,
            IOptions<MoveOptions> options,
            ILogger<MoveApp> logger)
        {
            _registrar = registrar;
            _options = options.Value;
            _logger = logger;
        }

        public Task RunAsync(CancellationToken token)
        {
            if (!_registrar.IsRegistered)
            {
                _logger.LogError("No msbuild could be found!");
                return Task.CompletedTask;
            }

            var projectCollection = new ProjectCollection();
            var sln = Microsoft.Build.Construction.SolutionFile.Parse(_options.SolutionPath);

            foreach (var project in sln.ProjectsInOrder)
            {
                if (project.ProjectType == Microsoft.Build.Construction.SolutionProjectType.KnownToBeMSBuildFormat)
                {
                    _logger.LogTrace("Loading {Project}", project.ProjectName);
                    projectCollection.LoadProject(project.AbsolutePath);
                }
                else if (project.ProjectType != Microsoft.Build.Construction.SolutionProjectType.SolutionFolder)
                {
                    _logger.LogWarning("Skipping {Project}", project.ProjectName);
                }
            }

            var projects = projectCollection.GetLoadedProjects(_options.ProjectPath);

            if (projects.Count != 1)
            {
                _logger.LogWarning("Could not find project in solution.");
                return Task.CompletedTask;
            }

            MoveProject(projects.First());
            return Task.CompletedTask;
        }

        private void MoveProject(Project project)
        {

        }
    }
}
