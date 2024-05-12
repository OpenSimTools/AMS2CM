using Core.Utils;

namespace Core.Mods;

public abstract class ExtractedMod : IMod
{
    public const string RemoveFileSuffix = "-remove";

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
            InstallFiles(rootPath, dstPath,
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

    private static void InstallFiles(string srcPath, string dstPath, ProcessingCallbacks<string> callbacks) =>
        RecursiveMoveWithBackup(srcPath, srcPath, dstPath, callbacks);

    private static void RecursiveMoveWithBackup(string rootPath, string srcPath, string dstPath, ProcessingCallbacks<string> callbacks)
    {
        if (!Directory.Exists(dstPath))
        {
            Directory.CreateDirectory(dstPath);
        }

        foreach (var maybeSrcSubPath in Directory.GetFileSystemEntries(srcPath))
        {
            var (srcSubPath, remove) = NeedsRemoving(maybeSrcSubPath);

            var localName = Path.GetFileName(srcSubPath);

            var dstSubPath = Path.Combine(dstPath, localName);
            if (Directory.Exists(srcSubPath)) // Is directory
            {
                RecursiveMoveWithBackup(rootPath, srcSubPath, dstSubPath, callbacks);
                continue;
            }

            var relativePath = Path.GetRelativePath(rootPath, srcSubPath);
            if (!callbacks.Accept(relativePath))
            {
                callbacks.NotAccepted(relativePath);
                continue;
            }

            callbacks.Before(relativePath);

            if (!remove)
            {
                File.Move(srcSubPath, dstSubPath);
            }

            callbacks.After(relativePath);
        }
    }

    private static (string, bool) NeedsRemoving(string filePath)
    {
        return filePath.EndsWith(RemoveFileSuffix) ?
            (filePath.RemoveSuffix(RemoveFileSuffix).Trim(), true) :
            (filePath, false);
    }


    protected abstract IEnumerable<string> ExtractedRootDirs();

    protected abstract ConfigEntries GenerateConfig();

    // **********************************************************************************
    // TODO this should be moved to the ModManager since it's only about config exclusion
    // **********************************************************************************
    protected virtual bool FileShouldBeInstalled(string relativePath) => true;
}