namespace Core.Utils;

/// <summary>
/// A canonical directory boundary, without resolving symbolic links or junctions.
/// </summary>
public sealed record RootPath
{
    public string Full { get; }

    public RootPath(string path)
    {
        Full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    public RootedPath Combine(string path) => new(this, path);

    public RootedPath Combine(params string[] subPaths) => Combine(Path.Combine(subPaths));

    public bool Equals(RootPath? other) =>
        other is not null && StringComparer.OrdinalIgnoreCase.Equals(Full, other.Full);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Full);
}
