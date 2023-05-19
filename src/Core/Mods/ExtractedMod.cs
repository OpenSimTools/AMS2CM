namespace Core.Mods;

internal abstract class ExtractedMod : IMod
{
    protected static readonly IMod.ConfigEntries EmptyConfig =
        new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    protected readonly string extractedPath;
    protected readonly List<string> installedFiles = new();

    public ExtractedMod(string packageName, string extractedPath)
    {
        PackageName = packageName;
        Config = EmptyConfig;
        this.extractedPath = extractedPath;
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

    public IReadOnlyCollection<string> InstalledFiles => installedFiles;

    public void Install(string dstPath)
    {
        if (Installed != IMod.InstalledState.NotInstalled)
        {
            throw new InvalidOperationException();
        }
        Installed = IMod.InstalledState.PartiallyInstalled;

        foreach (var rootPath in ExtractedRootDirs())
        {
            JsgmeFileInstaller.InstallFiles(rootPath, dstPath,
                relativeFilePath => installedFiles.Add(relativeFilePath));
        }

        Config = GenerateConfig();

        Installed = IMod.InstalledState.Installed;
    }

    protected abstract IEnumerable<string> ExtractedRootDirs();

    protected abstract IMod.ConfigEntries GenerateConfig();
}