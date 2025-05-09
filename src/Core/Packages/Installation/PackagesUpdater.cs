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
        Action<string, PackageInstallationState?> updatePackageState,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        UninstallPackages(currentState, installers, installDir, updatePackageState, eventHandler, cancellationToken);
        InstallPackages(currentState, installers, installDir, updatePackageState, eventHandler, cancellationToken);
    }

    // TODO TODO TODO TODO TODO TODO TODO TODO TODO TODO TODO TODO TODO TODO
    // - [x] Remove all mod files that have a different version or not to be installed
    // - [ ] Remove duplicate files in multiple mods (from last to first)
    // Notes:
    // - When a mod shadows another, we should create a dependency between the two so
    //   that the dependency being partial makes the mod partial as well
    private void UninstallPackages(
        IReadOnlyDictionary<string, PackageInstallationState> currentState,
        IReadOnlyCollection<IInstaller> installers,
        string installDir,
        Action<string, PackageInstallationState?> updatePackageState,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        if (currentState.Any())
        {
            eventHandler.UninstallStart();
            var fsHashesToInstall = installers.ToDictionary(i => i.PackageName, i => i.PackageFsHash);
            foreach (var (packageName, packageInstallationState) in currentState)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                // Skip packages at the same version
                var fsHashToInstall = fsHashesToInstall.GetValueOrDefault(packageName);
                if (fsHashToInstall != null && fsHashToInstall == packageInstallationState.FsHash)
                {
                    continue;
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
                    if (filesLeft.Count == 0)
                    {
                        updatePackageState(packageName, null);
                    }
                    else
                    {
                        updatePackageState(packageName, packageInstallationState with
                        {
                            Partial = true,
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

    // TODO TODO TODO TODO TODO TODO TODO TODO TODO TODO TODO TODO TODO TODO
    // - [ ] Go through all mods to be installed and extract files...
    //   - Skip if file already installed in this loop
    //   - Otherwise install with backup
    // - [ ] If files are shadowed by a mod, mark it as a dependency
    //   - We might have to pass the package name in the callback to keep track of that
    //     since it's a callback in the installation loop
    // Notes:
    // - If files have been installed previously by a mod, they will be either installed
    //   by a higher priority mod or skipped and not appear in the installed files
    //   (not sure what this meant)
    // - When can we skip mod installation entirely?
    //   - When it does not depend on a mod that was removed?
    private void InstallPackages(
        IReadOnlyDictionary<string, PackageInstallationState> currentState,
        IReadOnlyCollection<IInstaller> installers,
        string installDir,
        Action<string, PackageInstallationState?> updatePackageState,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        // foreach (var installer in installers.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        // {
        //     var installationState = currentState.GetValueOrDefault(installer.PackageName);
        //     var backupStrategy = backupStrategyProvider.BackupStrategy(installationState);
        //     try
        //     {
        //         installer.Install(InstallTo(installDir), backupStrategy, SkipAlreadyInstalledFiles());
        //     }
        //     finally
        //     {
        //         var packageInstalledFiles = installer.InstalledFiles
        //             .Where(rp => rp.Root == installDir)
        //             .Select(rp => rp.Relative)
        //             .ToImmutableList();
        //         updatePackageState(installer.PackageName,
        //             packageInstalledFiles.Count == 0
        //                 ? null
        //                 : new PackageInstallationState(
        //                     Time: timeProvider.GetUtcNow().DateTime,
        //                     FsHash: installer.PackageFsHash,
        //                     Partial: installer.Installed == IInstallation.State.PartiallyInstalled,
        //                     Files: packageInstalledFiles
        //                 ));
        //     }
        // }
        //
        // ProcessingCallbacks<RootedPath> SkipAlreadyInstalledFiles()
        // {
        //     var alreadyInstalledFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        //     return new ProcessingCallbacks<RootedPath>
        //     {
        //         Accept = gamePath => !alreadyInstalledFiles.Contains(gamePath.Relative),
        //         Before = gamePath => alreadyInstalledFiles.Add(gamePath.Relative)
        //     };
        // }

        // OLD CODE FOLLOWS OLD CODE FOLLOWS OLD CODE FOLLOWS OLD CODE FOLLOWS
        // OLD CODE FOLLOWS OLD CODE FOLLOWS OLD CODE FOLLOWS OLD CODE FOLLOWS
        // OLD CODE FOLLOWS OLD CODE FOLLOWS OLD CODE FOLLOWS OLD CODE FOLLOWS
        // OLD CODE FOLLOWS OLD CODE FOLLOWS OLD CODE FOLLOWS OLD CODE FOLLOWS
        // OLD CODE FOLLOWS OLD CODE FOLLOWS OLD CODE FOLLOWS OLD CODE FOLLOWS
        var allInstalledFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                var automaticDependencies = new HashSet<string>();
                var installCallbacks = new ProcessingCallbacks<RootedPath>
                {
                    Accept = gamePath =>
                    {
                        var overridingPackageName = allInstalledFiles.GetValueOrDefault(gamePath.Relative);
                        if (overridingPackageName is null)
                        {
                            return true;
                        }
                        if (overridingPackageName != installer.PackageName)
                        {
                            automaticDependencies.Add(overridingPackageName);
                        }
                        return false;
                    },
                    Before = gamePath => allInstalledFiles.Add(gamePath.Relative, installer.PackageName)
                };
                try
                {
                    installer.Install(InstallTo(installDir), backupStrategy, installCallbacks);
                }
                finally
                {
                    var packageInstalledFiles = installer.InstalledFiles
                        .Where(rp => rp.Root == installDir)
                        .Select(rp => rp.Relative)
                        .ToImmutableList();
                    automaticDependencies.UnionWith(installer.PackageDependencies);
                    updatePackageState(installer.PackageName,
                        packageInstalledFiles.Count == 0
                            ? null
                            : new PackageInstallationState(
                                Time: timeProvider.GetUtcNow().DateTime,
                                FsHash: installer.PackageFsHash,
                                Partial: installer.Installed == IInstallation.State.PartiallyInstalled,
                                Dependencies: automaticDependencies,
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
