using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PackageVersionUpdater
{
    public class RemoveCentralPackageManagementUpdater : IApplication
    {
        private readonly Registrar _registrar;
        private readonly ILogger<CentralPackageUpdater> _logger;
        private readonly UpdaterOptions _options;

        public RemoveCentralPackageManagementUpdater(Registrar registrar, ILogger<CentralPackageUpdater> logger, IOptions<UpdaterOptions> options)
        {
            _registrar = registrar;
            _logger = logger;
            _options = options.Value;
        }

        public Task RunAsync(CancellationToken token)
        {
            if (!_registrar.IsRegistered)
            {
                _logger.LogError("No msbuild could be found!");
                return Task.CompletedTask;
            }

            var projectCollection = new ProjectCollection();
            var sln = Microsoft.Build.Construction.SolutionFile.Parse(_options.Path);

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

            var collection = new NuGetReferenceCollection(_logger, Path.GetDirectoryName(_options.Path));

            if (collection.Count == 0)
            {
                _logger.LogInformation("No centrally managed versions found");
                return Task.CompletedTask;
            }

            foreach (var project in projectCollection.LoadedProjects)
            {
                var name = Path.GetFileName(project.FullPath);

                _logger.LogInformation("Updating {Project} for NuGet versions", name);

                foreach (var reference in project.Items.Where(i => i.ItemType.Equals("PackageReference", System.StringComparison.OrdinalIgnoreCase)))
                {
                    if (!reference.IsImported && collection.TryGetValue(reference.EvaluatedInclude, out var version))
                    {
                        reference.SetMetadataValue("Version", version);
                    }
                }

                project.Save();
            }

            return Task.CompletedTask;
        }
    }
}
