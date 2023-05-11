namespace Core.Mods;

public abstract class ExtractedMod : IMod
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

    protected static readonly IMod.ConfigEntries EmptyConfig =
        new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    protected readonly string _extractedPath;
    protected readonly List<string> _installedFiles = new();

    public ExtractedMod(string packageName, string extractedPath)
    {
        PackageName = packageName;
        Config = EmptyConfig;
        _extractedPath = extractedPath;
    }

    public string PackageName
    {
        get;
    }

    public IMod.InstalledState Installed
    {
        get;
        private set;
    }

    public IMod.ConfigEntries Config
    {
        get;
        private set;
    }

    public IReadOnlyCollection<string> InstalledFiles => _installedFiles;

    public void Install(string dstPath)
    {
        if (Installed != IMod.InstalledState.NotInstalled)
        {
            throw new InvalidOperationException();
        }
        Installed = IMod.InstalledState.PartiallyInstalled;

        foreach (var rootPath in FindModRootDirs())
        {
            JsgmeFileInstaller.InstallFiles(rootPath, dstPath,
                relativeFilePath => _installedFiles.Add(relativeFilePath));
        }

        Config = GenerateConfig();

        Installed = IMod.InstalledState.Installed;
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
                return new List<string> { path };
            }
            roots.AddRange(FindRootContaining(subdir, contained));
        }

        return roots;
    }

    protected abstract IMod.ConfigEntries GenerateConfig();
}