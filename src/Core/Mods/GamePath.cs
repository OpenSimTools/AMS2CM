namespace Core.Mods;

public class GamePath
{
    public string Relative { get; }
    public string Full { get; }

    public GamePath(string rootPath, string relativePath)
    {
        Relative = relativePath;
        Full = Path.Combine(rootPath, relativePath);
    }
}
