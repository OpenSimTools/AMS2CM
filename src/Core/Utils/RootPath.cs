using System.IO.Abstractions;

namespace Core.Utils;

/// <summary>
/// A canonical directory boundary, without resolving symbolic links or junctions.
/// </summary>
public sealed record RootPath
{
    public string Full { get; }
    public IDirectoryInfo Directory { get; }
    public IFileSystem FileSystem { get; }

    /// <summary>
    /// Creates a root path from a root directory and, optionally, some sub-paths.
    /// </summary>
    /// <param name="directory">Root directory.</param>
    /// <param name="subDirectories">Additional paths to combine with the root directory.</param>
    public RootPath(IDirectoryInfo directory, params string[] subDirectories)
    {
        Directory = directory;
        FileSystem = directory.FileSystem;
        var fullPath = Path.GetFullPath(Path.Combine(subDirectories), directory.FullName);
        Full = Path.TrimEndingDirectorySeparator(fullPath);
    }

    public RootedPath Combine(string relative) => new(this, relative);

    public RootedPath Combine(params string[] relatives) => Combine(Path.Combine(relatives));

    public bool Equals(RootPath? other) =>
        other is not null && StringComparer.OrdinalIgnoreCase.Equals(Full, other.Full);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Full);
}
