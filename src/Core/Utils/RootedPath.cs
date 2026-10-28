namespace Core.Utils;

/// <summary>
/// A canonical path within a fixed root, without resolving symbolic links or junctions.
/// </summary>
public sealed record RootedPath
{
    public PathRoot Root { get; }
    public string Relative { get; }
    public string Full => Path.Combine(Root.Full, Relative);

    public RootedPath(PathRoot root, string relative = "")
        : this(root, root.Full, relative)
    {
    }

    private RootedPath(PathRoot root, string basePath, string relative)
    {
        Root = root;
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(relative, basePath));
        var relativePath = Path.GetRelativePath(Root.Full, fullPath);
        if (Path.IsPathRooted(relativePath) || relativePath == ".." ||
            relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException("The path resolves outside the root directory.");
        }
        Relative = relativePath == "." ? "" : relativePath;
    }

    public RootedPath Combine(string relative) =>
        new(Root, Full, relative);

    public RootedPath Combine(params string[] subPaths) => Combine(Path.Combine(subPaths));

    /// <summary>
    /// Returns the parent within the original root, or null at the root.
    /// </summary>
    public RootedPath? Parent() => Relative.Length == 0 ? null : Combine("..");

    /// <summary>
    /// Compares canonical roots and relative paths without case sensitivity.
    /// </summary>
    public bool Equals(RootedPath? other) =>
        other is not null &&
        Root == other.Root &&
        StringComparer.OrdinalIgnoreCase.Equals(Relative, other.Relative);

    public override int GetHashCode() =>
        HashCode.Combine(
            Root,
            StringComparer.OrdinalIgnoreCase.GetHashCode(Relative));
}
