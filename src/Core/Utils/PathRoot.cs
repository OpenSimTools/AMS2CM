using System.IO.Abstractions;

namespace Core.Utils;

/// <summary>
/// A canonical directory boundary, without resolving symbolic links or junctions.
/// </summary>
public sealed record PathRoot
{
    public string Full { get; }
    public IDirectoryInfo Directory { get; }
    public IFileSystem FileSystem { get; }

    /// <summary>
    /// Creates a path root from a base directory and, optionally, some subpaths.
    /// </summary>
    /// <param name="directory">Base directory.</param>
    /// <param name="subpaths">Additional paths to combine with the base directory.</param>
    public PathRoot(IDirectoryInfo directory, params string[] subpaths)
    {
        Directory = directory;
        FileSystem = directory.FileSystem;
        var fullPath = Path.GetFullPath(Path.Combine(subpaths), directory.FullName);
        Full = Path.TrimEndingDirectorySeparator(fullPath);
    }

    public RootedPath Combine(string relative) => new(this, relative);

    public RootedPath Combine(params string[] relatives) => Combine(Path.Combine(relatives));

    public bool Equals(PathRoot? other) =>
        other is not null && StringComparer.OrdinalIgnoreCase.Equals(Full, other.Full);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Full);
}
