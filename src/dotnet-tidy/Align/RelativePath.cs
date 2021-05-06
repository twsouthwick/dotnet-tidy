using System;

namespace PackageVersionUpdater
{
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
}
