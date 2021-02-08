using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PackageVersionUpdater
{
    public partial class PackageUpdater
    {
        public class NuGetReferenceCollection
        {
            private readonly Dictionary<string, SemanticVersion> _versions;
            private readonly ILogger _logger;
            private readonly string _path;
            private readonly LinkedList<Action> _remove;

            public NuGetReferenceCollection(ILogger logger, string directory)
            {
                _logger = logger;
                _path = Path.Combine(directory, "Directory.Packages.props");
                _versions = new Dictionary<string, SemanticVersion>();
                _remove = new LinkedList<Action>();
            }

            public void Add(string projectName, string packageName, string version, Action remove)
            {
                var packageVersion = SemanticVersion.Parse(version);

                if (_versions.TryGetValue(packageName, out var existing))
                {
                    var compared = existing.CompareTo(packageVersion);

                    if (compared < 0)
                    {
                        _logger.LogWarning("{PackageName} is out of date for a project ({Version} < {NewVersion})", packageName, existing, version);
                        _versions[packageName] = packageVersion;
                    }
                }
                else
                {
                    _versions.Add(packageName, packageVersion);
                }

                _remove.AddLast(remove);
            }

            public IEnumerable<(string Name, SemanticVersion Version)> Packages => _versions
                .OrderBy(v => v.Key)
                .Select(v => (v.Key, v.Value));

            public void CreatePackageVersionFile()
            {
                var versions = Packages
                    .Select(v => new XElement("PackageVersion", new XAttribute("Include", v.Name), new XAttribute("Version", v.Version)));

                var project = new XElement("Project", new XElement("ItemGroup", versions));

                project.Save(_path);

                foreach (var remove in _remove)
                {
                    remove();
                }

                _remove.Clear();

            }
        }
    }
}
