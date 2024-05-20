namespace Core.Mods;

public class RootedPath
{
    public string Relative { get; }
    public string Full { get; }

    public RootedPath(string rootPath, string relativePath)
    {
        Relative = relativePath;
        Full = Path.Combine(rootPath, relativePath);
    }
}
