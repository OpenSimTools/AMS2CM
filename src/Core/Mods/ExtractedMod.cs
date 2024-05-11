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

    public IModInstallation.State Installed
    {
        get;
        private set;
    }

    public IReadOnlyCollection<string> InstalledFiles => installedFiles;

    public ConfigEntries Install(string dstPath, ProcessingCallbacks<string> callbacks)
    {
        if (Installed != IModInstallation.State.NotInstalled)
        {
            throw new InvalidOperationException();
        }
        Installed = IModInstallation.State.PartiallyInstalled;

        var now = DateTime.UtcNow;
        foreach (var rootPath in ExtractedRootDirs())
        {
            JsgmeFileInstaller.InstallFiles(rootPath, dstPath,
                callbacks
                    .AndAccept(FileShouldBeInstalled)
                    .AndAfter(relativePath =>
                    {
                        installedFiles.Add(relativePath);
                        // TODO This should be moved out to where we skip backup if created after
                        var fullPath = Path.Combine(dstPath, relativePath);
                        if (File.Exists(fullPath) && File.GetCreationTimeUtc(fullPath) > now)
                        {
                            File.SetCreationTimeUtc(fullPath, now);
                        }
                    })
            );
        }
        Installed = IModInstallation.State.Installed;

        return GenerateConfig();
    }

    protected abstract IEnumerable<string> ExtractedRootDirs();

    protected abstract ConfigEntries GenerateConfig();

    // **********************************************************************************
    // TODO this should be moved to the ModManager since it's only about config exclusion
    // **********************************************************************************
    protected virtual bool FileShouldBeInstalled(string relativePath) => true;
}