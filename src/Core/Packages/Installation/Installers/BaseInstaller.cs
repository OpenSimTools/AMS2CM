using Core.Packages.Installation.Backup;
using Core.Utils;

namespace Core.Packages.Installation.Installers;

/// <summary>
///
/// </summary>
/// <typeparam name="TPassthrough">Type used by the implementation during the install loop.</typeparam>
internal abstract class BaseInstaller<TPassthrough> : IInstaller
{
    public string PackageName { get; }
    public int? PackageFsHash { get; }

    public IInstallation.State Installed { get; private set; }
    public IReadOnlyCollection<RootedPath> InstalledFiles => installedFiles;

    private readonly List<RootedPath> installedFiles = new();

    protected BaseInstaller(string packageName, int? packageFsHash)
    {
        PackageName = packageName;
        PackageFsHash = packageFsHash;
    }

    public void Install(IInstaller.Destination destination, IBackupStrategy backupStrategy, ProcessingCallbacks<RootedPath> callbacks)
    {
        if (Installed != IInstallation.State.NotInstalled)
        {
            throw new InvalidOperationException();
        }
        Installed = IInstallation.State.PartiallyInstalled;

        InstalAllFiles((pathInPackage, context) =>
        {
            var (destPath, removeFile) = NeedsRemoving(destination(pathInPackage));

            if (callbacks.Accept(destPath))
            {
                callbacks.Before(destPath);
                try
                {
                    backupStrategy.PerformBackup(destPath);
                    if (!removeFile)
                    {
                        Directory.GetParent(destPath.Full)?.Create();
                        InstallFile(destPath, context);
                    }
                }
                finally
                {
                    installedFiles.Add(destPath);
                }
                backupStrategy.AfterInstall(destPath);
                callbacks.After(destPath);
            }
            else
            {
                callbacks.NotAccepted(destPath);
            }
        });

        Installed = IInstallation.State.Installed;
    }

    /// <summary>
    /// Directories, relative to the source root.
    /// </summary>
    public abstract IEnumerable<string> RelativeDirectoryPaths { get; }

    /// <summary>
    /// Installation loop.
    /// </summary>
    /// <param name="body">Function to call for each file.</param>
    protected abstract void InstalAllFiles(InstallBody body);

    protected delegate void InstallBody(string relativePathInMod, TPassthrough context);

    protected abstract void InstallFile(RootedPath destinationPath, TPassthrough context);

    private static (RootedPath, bool) NeedsRemoving(RootedPath destPath)
    {
        var (relativePath, remove) = destPath.Relative.EndsWith(BaseInstaller.RemoveFileSuffix) ?
            (destPath.Relative.RemoveSuffix(BaseInstaller.RemoveFileSuffix).Trim(), true) :
            (destPath.Relative, false);
        return (new RootedPath(destPath.Root, relativePath), remove);
    }
}

public static class BaseInstaller
{
    public const string RemoveFileSuffix = "-remove";
}
