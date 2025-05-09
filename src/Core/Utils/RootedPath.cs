namespace Core.Utils;

public record RootedPath
{
    public string Root { get; }
    public string Relative { get; }
    public string Full { get; }

    public RootedPath(string rootPath, string relativePath = "")
    {
        Root = rootPath;
        Relative = relativePath;
        Full = Path.Combine(rootPath, relativePath);
    }

    public RootedPath SubPath(string subPath) =>
        new(Root, Path.Combine(Relative, subPath));
}
