using Microsoft.Build.Evaluation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PackageVersionUpdater
{
    public class AlignSolutionApp : IApplication
    {
        private readonly Registrar _registrar;
        private readonly AlignOptions _options;
        private readonly ILogger<AlignSolutionApp> _logger;

        public AlignSolutionApp(
            Registrar registrar,
            IOptions<AlignOptions> options,
            ILogger<AlignSolutionApp> logger)
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

            //Run();
            while (Run())
            {
            }

            return Task.CompletedTask;
        }

        private bool Run()
        {
            using var projectCollection = new ProjectCollection();
            var sln = Microsoft.Build.Construction.SolutionFile.Parse(_options.SolutionPath);
            var d = new Dictionary<string, (RelativePath Expected, RelativePath Actual)>();

            string GetParentPath(string guid)
            {
                if (guid is null)
                {
                    return string.Empty;
                }

                var p = sln.ProjectsByGuid[guid];

                return Path.Combine(GetParentPath(p.ParentProjectGuid), p.ProjectName);
            }

            foreach (var project in sln.ProjectsInOrder)
            {
                if (project.ProjectType == Microsoft.Build.Construction.SolutionProjectType.KnownToBeMSBuildFormat)
                {
                    var dir = Path.Combine(GetParentPath(project.ProjectGuid), Path.GetFileName(project.AbsolutePath));
                    var actual = new RelativePath(project.RelativePath);
                    var expected = new RelativePath(dir);

                    if (!expected.Equals(actual))
                    {
                        d.Add(project.ProjectName, (expected, actual));
                    }

                    projectCollection.LoadProject(project.AbsolutePath);
                }
                else if (project.ProjectType != Microsoft.Build.Construction.SolutionProjectType.SolutionFolder)
                {
                    _logger.LogWarning("Skipping {Project}", project.ProjectName);
                }
            }

            var slnDir = Path.GetDirectoryName(_options.SolutionPath);

            foreach (var project in projectCollection.LoadedProjects)
            {
                var projectName = Path.GetFileNameWithoutExtension(project.FullPath);

                if (!d.TryGetValue(projectName, out var result))
                {
                    continue;
                }

                _logger.LogInformation("Updating directory for {Project}", Path.GetFileName(project.FullPath));

                var (expectedRelative, actualRelative) = result;
                var expected = new AbsolutePath(slnDir, expectedRelative);
                var actual = new AbsolutePath(slnDir, actualRelative);

                var parentDir = Directory.GetParent(expected.Path).Parent;
                if (!parentDir.Exists)
                {
                    parentDir.Create();
                }

                var slnFile = File.ReadAllText(_options.SolutionPath);
                var updatedSln = slnFile.Replace(actualRelative.Path, expectedRelative.Path);
                File.WriteAllText(_options.SolutionPath, updatedSln);

                MoveProject(projectCollection, expected.Path, actual.Path);

                Directory.Move(project.DirectoryPath, Path.GetDirectoryName(expected.Path));

                return true;
            }

            return false;
        }

        private void MoveProject(ProjectCollection projects, string dest, string actual)
        {
            foreach (var p in projects.LoadedProjects)
            {
                if (p.FullPath.Contains("console", StringComparison.OrdinalIgnoreCase))
                {

                }
                foreach (var r in p.GetItems("ProjectReference"))
                {
                    var fullPath = Path.GetFullPath(r.EvaluatedInclude, p.DirectoryPath);

                    if (string.Equals(p.FullPath, actual, StringComparison.OrdinalIgnoreCase))
                    {
                        r.UnevaluatedInclude = Path.GetRelativePath(Path.GetDirectoryName(dest), fullPath);
                    }
                    else if (string.Equals(fullPath, actual, StringComparison.OrdinalIgnoreCase))
                    {
                        r.UnevaluatedInclude = Path.GetRelativePath(p.DirectoryPath, dest);
                    }
                    {

                    }
                }

                p.Save();
            }
        }
    }
}
