using Microsoft.Build.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PackageVersionUpdater
{
    public static class MoveExtensions
    {
        public static void AddMoveHelpers(this IServiceCollection services, Action<MoveOptions> configure)
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

        private ProjectCollection LoadCollection()
        {
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

            return projectCollection;
        }

        public Task RunAsync(CancellationToken token)
        {
            if (!_registrar.IsRegistered)
            {
                _logger.LogError("No msbuild could be found!");
                return Task.CompletedTask;
            }

            var projectCollection = LoadCollection();
            var projects = projectCollection.GetLoadedProjects(_options.ProjectPath);

            if (projects.Count != 1)
            {
                _logger.LogWarning("Could not find project in solution.");
                return Task.CompletedTask;
            }

            MoveProject(projectCollection, projects.First());
            return Task.CompletedTask;
        }

        private void MoveProject(ProjectCollection projects, Project project)
        {
            var originalName = Path.GetFileName(project.FullPath);
            var expectedName = Path.GetFileName(_options.DestinationDirectory);
            var expectedProjectFile = string.Concat(expectedName, Path.GetExtension(originalName));
            var updatedPath = Path.Combine(_options.DestinationDirectory, expectedProjectFile);

            var parentDir = Directory.GetParent(_options.DestinationDirectory);
            if (!parentDir.Exists)
            {
                parentDir.Create();
            }

            Directory.Move(project.DirectoryPath, _options.DestinationDirectory);
            File.Move(Path.Combine(_options.DestinationDirectory, originalName), updatedPath);

            var sln = File.ReadAllText(_options.SolutionPath);
            var updatedSln = sln.Replace(Path.GetFileNameWithoutExtension(originalName), expectedName);
            File.WriteAllText(_options.SolutionPath, updatedSln);

            foreach (var p in projects.LoadedProjects)
            {
                foreach (var r in p.GetItems("ProjectReference"))
                {
                    var fullPath = Path.GetFullPath(r.EvaluatedInclude, p.DirectoryPath);

                    if (string.Equals(project.FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var path = Path.GetRelativePath(p.DirectoryPath, updatedPath);
                        r.UnevaluatedInclude = path;
                    }
                }

                p.Save();
            }
        }
    }
}
