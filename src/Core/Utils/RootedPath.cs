namespace Core.Utils;

public sealed record RootedPath(string Root, string Relative = "")
{
    public string Full { get; } = Path.Combine(Root, Relative);

    public RootedPath SubPath(string subPath) =>
        new(Root, Path.Combine(Relative, subPath));

    /// <summary>
    /// Compares full paths without case sensitivity or additional path normalization.
    /// </summary>
    public bool Equals(RootedPath? other) =>
        other is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(Full, other.Full);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Full);
}
