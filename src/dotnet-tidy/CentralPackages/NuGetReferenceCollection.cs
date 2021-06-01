using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PackageVersionUpdater
{
    public class NuGetReferenceCollection : IReadOnlyDictionary<string, string>
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

            TryLoad();
        }

        private void TryLoad()
        {
            _logger.LogError(_path);
            if (!File.Exists(_path))
            {
                return;
            }

            var doc = XDocument.Load(_path);
            var items = doc.Root.Descendants("PackageVersion");

            foreach (var item in items)
            {
                var include = item.Attribute("Include")?.Value;
                var version = item.Attribute("Version")?.Value;

                if (include is not null && version is not null)
                {
                    Add(include, version, static () => { });
                }
            }
        }

        public void Add(string packageName, string version, Action remove)
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

        public IEnumerable<string> Keys => _versions.Keys;

        public IEnumerable<string> Values => _versions.Values.Select(v => v.ToFullString());

        public int Count => _versions.Count;

        public string this[string key] => _versions[key].ToFullString();

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

        public bool ContainsKey(string key) => _versions.ContainsKey(key);

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value)
        {
            if (_versions.TryGetValue(key, out var result))
            {
                value = result.ToFullString();
                return true;
            }

            value = null;
            return false;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            foreach (var item in _versions)
            {
                yield return new KeyValuePair<string, string>(item.Key, item.Value.ToFullString());
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
