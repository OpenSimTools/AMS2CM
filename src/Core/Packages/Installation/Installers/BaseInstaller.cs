using Core.Mods.Installation.Installers;
using Core.Packages.Installation.Backup;
using Core.Utils;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Core.Packages.Installation.Installers;

/// <summary>
///
/// </summary>
/// <typeparam name="TPassthrough">Type used by the implementation during the install loop.</typeparam>
internal abstract class BaseInstaller<TPassthrough> : IInstaller
{
    protected readonly DirectoryInfo StagingDir;

    public string PackageName { get; }
    public int? PackageFsHash { get; }

    public IInstallation.State Installed { get; private set; }
    public IReadOnlyCollection<string> InstalledFiles => installedFiles;

    private readonly IRootFinder rootFinder;
    private readonly Matcher filesToInstallMatcher;
    private readonly List<string> installedFiles = new();

    internal BaseInstaller(string packageName, int? packageFsHash, ITempDir tempDir, BaseInstaller.IConfig config)
    {
        PackageName = packageName;
        PackageFsHash = packageFsHash;
        StagingDir = new DirectoryInfo(Path.Combine(tempDir.BasePath, packageName));
        rootFinder = new ContainedDirsRootFinder(config.DirsAtRoot);
        filesToInstallMatcher = Matchers.ExcludingPatterns(config.ExcludedFromInstall);
    }

    public void Install(string dstPath, IInstallationBackupStrategy backupStrategy, ProcessingCallbacks<RootedPath> callbacks)
    {
        if (Installed != IInstallation.State.NotInstalled)
        {
            throw new InvalidOperationException();
        }
        Installed = IInstallation.State.PartiallyInstalled;

        var rootPaths = rootFinder.FromDirectoryList(RelativeDirectoryPaths);

        InstalAllFiles((pathInPackage, context) =>
        {
            var relativePathInMod = rootPaths.GetPathFromRoot(pathInPackage);

            // If not part of any game root
            if (relativePathInMod is null)
            {
                // Config files only at the mod root
                if (!pathInPackage.Contains(Path.DirectorySeparatorChar))
                {
                    var modConfigDstPath = new RootedPath(StagingDir.FullName, pathInPackage);
                    Directory.GetParent(modConfigDstPath.Full)?.Create();
                    InstallFile(modConfigDstPath, context);
                }
                return;
            }

            var (relativePath, removeFile) = NeedsRemoving(relativePathInMod);

            var gamePath = new RootedPath(dstPath, relativePath);

            if (Whitelisted(gamePath) && callbacks.Accept(gamePath))
            {
                callbacks.Before(gamePath);
                backupStrategy.PerformBackup(gamePath);
                if (!removeFile)
                {
                    Directory.GetParent(gamePath.Full)?.Create();
                    InstallFile(gamePath, context);
                }
                installedFiles.Add(gamePath.Relative);
                backupStrategy.AfterInstall(gamePath);
                callbacks.After(gamePath);
            }
            else
            {
                callbacks.NotAccepted(gamePath);
            }
        });

        Installed = IInstallation.State.Installed;
    }

    /// <summary>
    /// Mod directories, relative to the source root.
    /// </summary>
    protected abstract IEnumerable<string> RelativeDirectoryPaths { get; }

    /// <summary>
    /// Installation loop.
    /// </summary>
    /// <param name="body">Function to call for each file.</param>
    protected abstract void InstalAllFiles(InstallBody body);

    protected delegate void InstallBody(string relativePathInMod, TPassthrough context);

    protected abstract void InstallFile(RootedPath destinationPath, TPassthrough context);

    private bool Whitelisted(RootedPath path) =>
        filesToInstallMatcher.Match(path.Relative).HasMatches;

    private static (string, bool) NeedsRemoving(string filePath)
    {
        return filePath.EndsWith(BaseInstaller.RemoveFileSuffix) ?
            (filePath.RemoveSuffix(BaseInstaller.RemoveFileSuffix).Trim(), true) :
            (filePath, false);
    }
}

public static class BaseInstaller
{
    public interface IConfig
    {
        IEnumerable<string> DirsAtRoot
        {
            get;
        }

        IEnumerable<string> ExcludedFromInstall
        {
            get;
        }
    }

    public const string RemoveFileSuffix = "-remove";
}
