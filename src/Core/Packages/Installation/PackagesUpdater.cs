using System.Collections.Immutable;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Packages.Repository;
using Core.Utils;

namespace Core.Packages.Installation;

public class PackagesUpdater<TEventHandler>
    where TEventHandler : PackagesUpdater.IEventHandler
{
    private readonly IInstallerFactory installerFactory;
    private readonly IBackupStrategyProvider<PackageInstallationState> backupStrategyProvider;
    private readonly TimeProvider timeProvider;

    public PackagesUpdater(
        IInstallerFactory installerFactory,
        IBackupStrategyProvider<PackageInstallationState>  backupStrategyProvider,
        TimeProvider timeProvider)
    {
        this.installerFactory = installerFactory;
        this.backupStrategyProvider = backupStrategyProvider;
        this.timeProvider = timeProvider;
    }

    public void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> previousState,
        IEnumerable<Package> packages,
        string installDir,
        Action<IReadOnlyDictionary<string, PackageInstallationState>> afterInstall,
        TEventHandler eventHandler, CancellationToken cancellationToken)
    {
        var installers = packages.Select(installerFactory.PackageInstaller).ToImmutableArray();

        var currentState = new Dictionary<string, PackageInstallationState>(previousState);
        try
        {
            Apply(
                previousState,
                installers,
                installDir,
                (packageName, state) =>
                {
                    if (state is null)
                    {
                        currentState.Remove(packageName);
                    }
                    else
                    {
                        currentState[packageName] = state;
                    }
                },
                eventHandler,
                cancellationToken);
        }
        finally
        {
            afterInstall(currentState);
        }
    }

    protected virtual void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> currentState,
        IReadOnlyCollection<IInstaller> installers,
        string installDir,
        Action<string, PackageInstallationState?> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        UninstallPackages(currentState, installDir, afterInstall, eventHandler, cancellationToken);
        InstallPackages(installers, installDir, afterInstall, eventHandler, cancellationToken);
    }

    private void UninstallPackages(
        IReadOnlyDictionary<string, PackageInstallationState> currentState,
        string installDir,
        Action<string, PackageInstallationState?> afterUninstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        if (currentState.Any())
        {
            eventHandler.UninstallStart();
            foreach (var (packageName, packagePackageInstallationState) in currentState)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                eventHandler.UninstallCurrent(packageName);
                var backupStrategy = backupStrategyProvider.BackupStrategy(packagePackageInstallationState);
                var filesLeft = packagePackageInstallationState.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var relativePath in packagePackageInstallationState.Files)
                    {
                        var gamePath = new RootedPath(installDir, relativePath);
                        if (!backupStrategy.RestoreBackup(gamePath))
                        {
                            eventHandler.UninstallSkipModified(gamePath.Relative);
                        }
                        filesLeft.Remove(gamePath.Relative);
                    }
                    DeleteEmptyDirectories(installDir, packagePackageInstallationState.Files);
                }
                finally
                {
                    if (filesLeft.Count == 0)
                    {
                        afterUninstall(packageName, null);
                    }
                    else
                    {
                        afterUninstall(packageName, packagePackageInstallationState with
                        {
                            // // Once partially uninstalled, it will stay that way unless fully uninstalled
                            Partial = packagePackageInstallationState.Partial ||
                                      filesLeft.Count != packagePackageInstallationState.Files.Count,
                            Files = filesLeft
                        });
                    }
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
        string destinationDir,
        Action<string, PackageInstallationState?> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var allInstalledFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var installCallbacks = new ProcessingCallbacks<RootedPath>
        {
            Accept = gamePath => !allInstalledFiles.Contains(gamePath.Relative),
            Before = gamePath => allInstalledFiles.Add(gamePath.Relative)
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
                    installer.Install(InstallTo(destinationDir), backupStrategy, installCallbacks);
                }
                finally
                {
                    var packageInstalledFiles = installer.InstalledFiles
                        .Where(rp => rp.Root == destinationDir)
                        .Select(rp => rp.Relative)
                        .ToImmutableList();
                    afterInstall(installer.PackageName,
                        packageInstalledFiles.Count == 0
                            ? null
                            : new PackageInstallationState(
                                Time: timeProvider.GetUtcNow().DateTime,
                                FsHash: installer.PackageFsHash,
                                Partial: installer.Installed == IInstallation.State.PartiallyInstalled,
                                Dependencies: installer.PackageDependencies,
                                Files: packageInstalledFiles
                        ));
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

    private static IInstaller.Destination InstallTo(string destDir) =>
        relativePath => new RootedPath(destDir, relativePath);
}

public static class PackagesUpdater
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
}
