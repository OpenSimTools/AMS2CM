namespace Core;

public class ManualInstallMod : IMod
{
    private static readonly string[] DirsAtRootLowerCase =
    {
        "cameras",
        "characters",
        "effects",
        "gui",
        "pakfiles",
        "render",
        "text",
        "tracks",
        "upgrade",
        "vehicles"
    };
    
    private readonly string _packageName;
    private readonly string _extractedPath;
    private readonly List<string> _installedFiles = new();

    public ManualInstallMod(string packageName, string extractedPath)
    {
        _packageName = packageName;
        _extractedPath = extractedPath;
    }

    public string PackageName => _packageName;
    public bool Installed => _installedFiles.Any();
    public IReadOnlyCollection<string> InstalledFiles => _installedFiles;
    
    public void Install(string dstPath)
    {
        if (Installed)
            throw new InvalidOperationException();

        foreach (var rootPath in FindModRootDirs())
        {
            JsgmeFileInstaller.InstallFiles(rootPath, dstPath,
                relativeFilePath => _installedFiles.Add(relativeFilePath));
        }
    }

    private IEnumerable<string> FindModRootDirs()
    {
        return FindRootContaining(_extractedPath, DirsAtRootLowerCase);
    }
    
    private static List<string> FindRootContaining(string path, string[] contained)
    {
        var roots = new List<string>();
        foreach (var subdir in Directory.GetDirectories(path))
        {
            var localName = Path.GetFileName(subdir).ToLowerInvariant();
            if (contained.Contains(localName))
            {
                return new List<string> {path};
            }
            roots.AddRange(FindRootContaining(subdir, contained));
        }

        return roots;
    }
}