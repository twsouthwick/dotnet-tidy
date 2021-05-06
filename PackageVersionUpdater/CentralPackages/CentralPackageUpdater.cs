using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PackageVersionUpdater
{
    public class CentralPackageUpdater : IApplication
    {
        private readonly Registrar _registrar;
        private readonly ILogger<CentralPackageUpdater> _logger;
        private readonly UpdaterOptions _options;

        public CentralPackageUpdater(Registrar registrar, ILogger<CentralPackageUpdater> logger, IOptions<UpdaterOptions> options)
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

            foreach (var project in projectCollection.LoadedProjects)
            {
                var name = Path.GetFileName(project.FullPath);

                _logger.LogInformation("Searching {Project} for NuGet references", name);

                foreach (var reference in project.Items.Where(i => i.ItemType.Equals("PackageReference", System.StringComparison.OrdinalIgnoreCase)))
                {
                    var version = reference.GetMetadataValue("Version");
                    var packageName = reference.EvaluatedInclude;

                    if (!reference.IsImported)
                    {
                        collection.Add(name, packageName, version, Remove);

                        void Remove()
                        {
                            reference.RemoveMetadata("Version");
                        }
                    }
                }
            }

            collection.CreatePackageVersionFile();

            foreach (var project in projectCollection.LoadedProjects)
            {
                project.Save();
            }

            return Task.CompletedTask;
        }
    }
}
