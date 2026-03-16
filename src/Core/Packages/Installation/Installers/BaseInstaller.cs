using System.Collections.Immutable;
using System.IO.Abstractions;
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

    public IReadOnlySet<string> PackageDependencies { get; }

    public IInstallation.State Installed { get; private set; }
    public IReadOnlySet<RootedPath> InstalledFiles => installedFiles;

    protected readonly IFileSystem FileSystem;

    private readonly HashSet<RootedPath> installedFiles = new();

    protected BaseInstaller(string packageName, int? packageFsHash)
        : this(packageName, packageFsHash, ImmutableHashSet<string>.Empty)
    {
    }

    protected BaseInstaller(string packageName, int? packageFsHash, IReadOnlySet<string> packageDependencies) :
        this(new FileSystem(), packageName, packageFsHash, packageDependencies)
    {
    }

    // A package cannot currently specify dependencies.
    protected BaseInstaller(IFileSystem fs, string packageName, int? packageFsHash, IReadOnlySet<string> packageDependencies)
    {
        FileSystem = fs;
        PackageName = packageName;
        PackageFsHash = packageFsHash;
        PackageDependencies = packageDependencies;
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

            callbacks.Wrap(() =>
            {
                try
                {
                    backupStrategy.PerformBackup(destPath);
                    if (!removeFile)
                    {
                        FileSystem.Directory.GetParent(destPath.Full)?.Create();
                        InstallFile(destPath, context);
                    }
                }
                finally
                {
                    installedFiles.Add(destPath);
                }
                backupStrategy.AfterInstall(destPath);
            }, destPath);
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
