namespace Core.Mods;

public abstract class ExtractedMod : IMod
{
    protected static readonly IMod.ConfigEntries EmptyConfigEntries =
        new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    protected readonly string extractedPath;
    protected readonly List<string> installedFiles = new();

    internal ExtractedMod(string packageName, string extractedPath)
    {
        PackageName = packageName;
        Config = EmptyConfigEntries;
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

    public void Install(string dstPath, JsgmeFileInstaller.BeforeFileCallback beforeFileCallback)
    {
        if (Installed != IMod.InstalledState.NotInstalled)
        {
            throw new InvalidOperationException();
        }
        Installed = IMod.InstalledState.PartiallyInstalled;

        var now = DateTime.UtcNow;
        foreach (var rootPath in ExtractedRootDirs())
        {
            JsgmeFileInstaller.InstallFiles(rootPath, dstPath,
                beforeFileCallback,
                relativePath =>
                {
                    installedFiles.Add(relativePath);
                    var fullPath = Path.Combine(dstPath, relativePath);
                    if (File.Exists(fullPath) && File.GetCreationTimeUtc(fullPath) > now)
                    {
                        File.SetCreationTimeUtc(fullPath, now);
                    }
                }
            );
        }

        Config = GenerateConfig();

        Installed = IMod.InstalledState.Installed;
    }

    protected abstract IEnumerable<string> ExtractedRootDirs();

    protected abstract IMod.ConfigEntries GenerateConfig();
}