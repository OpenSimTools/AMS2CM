using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Packages.Installation;

public class InstallationsUpdater : IInstallationsUpdater<InstallationsUpdater.IEventHandler>
{
    public interface IEventHandler : IProgress
    {
        void InstallNoPackages();
        void InstallStart();
        void InstallCurrent(string packageName);
        void InstallEnd();

        void UninstallNoPackages();
        void UninstallStart();
        void UninstallCurrent(string packageName);
        void UninstallSkipModified(string filePath);
        void UninstallEnd();
    }

    public interface IProgress
    {
        public void ProgressUpdate(IPercent? progress);
    }

    private readonly IBackupStrategyProvider backupStrategyProvider;

    public InstallationsUpdater(
        IBackupStrategy  backupStrategy)
    {
        backupStrategyProvider = new SkipUpdatedBackupStrategy.Provider(backupStrategy);
    }

    public void Apply(IReadOnlyDictionary<string, PackageInstallationState> currentState,
        IReadOnlyCollection<IInstaller> toInstall, string installDir, Action<IInstallation> afterInstall,
        IEventHandler eventHandler, CancellationToken cancellationToken)
    {
        UninstallPackages(currentState, installDir, afterInstall, eventHandler, cancellationToken);
        InstallPackages(toInstall, installDir, afterInstall, eventHandler, cancellationToken);
    }

    private void UninstallPackages(
        IReadOnlyDictionary<string, PackageInstallationState> currentState,
        string installDir,
        Action<IInstallation> afterUninstall,
        IEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        if (currentState.Any())
        {
            eventHandler.UninstallStart();
            foreach (var (packageName, packageInstallationState) in currentState)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                eventHandler.UninstallCurrent(packageName);
                var backupStrategy = backupStrategyProvider.BackupStrategy(packageInstallationState);
                var filesLeft = packageInstallationState.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var relativePath in packageInstallationState.Files)
                    {
                        var gamePath = new RootedPath(installDir, relativePath);
                        if (!backupStrategy.RestoreBackup(gamePath))
                        {
                            eventHandler.UninstallSkipModified(gamePath.Relative);
                        }
                        filesLeft.Remove(gamePath.Relative);
                    }
                    DeleteEmptyDirectories(installDir, packageInstallationState.Files);
                }
                finally
                {
                    var installationState = IInstallation.State.NotInstalled;
                    if (filesLeft.Count != 0)
                    {
                        // Once partially uninstalled, it will stay that way unless fully uninstalled
                        if (packageInstallationState.Partial || filesLeft.Count != packageInstallationState.Files.Count)
                        {
                            installationState = IInstallation.State.PartiallyInstalled;
                        }
                        else
                        {
                            installationState = IInstallation.State.Installed;
                        }
                    }

                    afterUninstall(new PackageInstallation(
                        packageName,
                        installationState,
                        filesLeft,
                        packageInstallationState.FsHash
                    ));
                }
            }
            eventHandler.UninstallEnd();
        }
        else
        {
            eventHandler.UninstallNoPackages();
        }
    }

    private static void DeleteEmptyDirectories(string dstRootPath, IReadOnlyCollection<string> filePaths)
    {
        var dirs = filePaths
            .Select(file => Path.Combine(dstRootPath, file))
            .SelectMany(dstFilePath => AncestorsUpTo(dstRootPath, dstFilePath))
            .Distinct()
            .OrderByDescending(name => name.Length);
        foreach (var dir in dirs)
        {
            // Some packages have duplicate entries, so files might have been removed already
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
            }
        }
    }

    private static List<string> AncestorsUpTo(string root, string path)
    {
        var ancestors = new List<string>();
        for (var dir = Directory.GetParent(path); dir is not null && dir.FullName != root; dir = dir.Parent)
        {
            ancestors.Add(dir.FullName);
        }
        return ancestors;
    }

    private void InstallPackages(
        IReadOnlyCollection<IInstaller> installers,
        string installDir,
        Action<IInstallation> afterInstall,
        IEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var installedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installCallbacks = new ProcessingCallbacks<RootedPath>
        {
            Accept = gamePath => !installedFiles.Contains(gamePath.Relative),
            Before = gamePath => installedFiles.Add(gamePath.Relative),
        };

        // Increase by one in case bootfiles are needed and another one to show that something is happening
        var progress = new PercentOfTotal(installers.Count + 2);
        if (installers.Any())
        {
            eventHandler.InstallStart();
            eventHandler.ProgressUpdate(progress.IncrementDone());

            foreach (var installer in installers.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
            {
                eventHandler.InstallCurrent(installer.PackageName);
                var backupStrategy = backupStrategyProvider.BackupStrategy(null);
                try
                {
                    installer.Install(installDir, backupStrategy, installCallbacks);
                }
                finally
                {
                    afterInstall(installer);
                }
                eventHandler.ProgressUpdate(progress.IncrementDone());
            }

            eventHandler.InstallEnd();
            eventHandler.ProgressUpdate(progress.IncrementDone());
        }
        else
        {
            eventHandler.InstallNoPackages();
        }
        eventHandler.ProgressUpdate(progress.DoneAll());
    }
}
