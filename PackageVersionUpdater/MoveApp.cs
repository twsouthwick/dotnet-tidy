using Microsoft.Build.Evaluation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
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

            Run();
            //while (Run())
            //{
            //}

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


        public readonly struct RelativePath : IEquatable<RelativePath>
        {
            public RelativePath(string path)
            {
                Path = path;
            }

            public string Path { get; }

            public RelativePath Append(string path) => new RelativePath(System.IO.Path.Combine(Path, path));

            public bool Equals(RelativePath other)
                => string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object obj) => obj is RelativePath other && Equals(other);

            public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Path);
        }

        public readonly struct AbsolutePath : IEquatable<AbsolutePath>
        {
            public AbsolutePath(string @base, RelativePath path)
            {
                Path = System.IO.Path.Combine(@base, path.Path);
            }

            public string Path { get; }

            public bool Equals(AbsolutePath other)
                => string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object obj) => obj is AbsolutePath other && Equals(other);

            public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Path);
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
