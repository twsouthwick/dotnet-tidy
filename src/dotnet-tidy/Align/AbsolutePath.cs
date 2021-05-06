using System;

namespace PackageVersionUpdater
{
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
}
