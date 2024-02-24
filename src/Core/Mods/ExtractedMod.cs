namespace Core.Mods;

public abstract class ExtractedMod : IMod
{
    protected readonly string extractedPath;
    protected readonly List<string> installedFiles = new();

    internal ExtractedMod(string packageName, int? packageFsHash, string extractedPath)
    {
        PackageName = packageName;
        PackageFsHash = packageFsHash;
        this.extractedPath = extractedPath;
    }

    public string PackageName
    {
        get;
    }

    public int? PackageFsHash
    {
        get;
    }

    public IMod.InstalledState Installed
    {
        get;
        private set;
    }

    public IReadOnlyCollection<string> InstalledFiles => installedFiles;

    public ConfigEntries Install(string dstPath, JsgmeFileInstaller.BeforeFileCallback beforeFileCallback)
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
                relativePath =>
                    FileShouldBeInstalled(relativePath) &&
                    beforeFileCallback(relativePath),
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
        Installed = IMod.InstalledState.Installed;

        return GenerateConfig();
    }

    protected abstract IEnumerable<string> ExtractedRootDirs();

    protected abstract ConfigEntries GenerateConfig();

    protected virtual bool FileShouldBeInstalled(string relativePath) => true;
}