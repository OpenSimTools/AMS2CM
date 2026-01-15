using System.Collections.Immutable;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Packages.Repository;
using Core.Utils;

namespace Core.Packages.Installation;

public class PackagesUpdater<TEventHandler> : IPackagesUpdater<TEventHandler>
    where TEventHandler : PackagesUpdater.IEventHandler
{
    private readonly IInstallerFactory installerFactory;
    private readonly IBackupStrategyProvider<PackageInstallationState, TEventHandler> backupStrategyProvider;
    private readonly TimeProvider timeProvider;

    public PackagesUpdater(
        IInstallerFactory installerFactory,
        IBackupStrategyProvider<PackageInstallationState, TEventHandler>  backupStrategyProvider,
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
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
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
            foreach (var (packageName, packageInstallationState) in currentState)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                eventHandler.UninstallCurrent(packageName);
                var backupStrategy = backupStrategyProvider.BackupStrategy(packageInstallationState, eventHandler);
                var filesLeft = packageInstallationState.Files.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var error = false;
                try
                {
                    foreach (var relativePath in packageInstallationState.Files)
                    {
                        var gamePath = new RootedPath(installDir, relativePath);
                        backupStrategy.RestoreBackup(gamePath);
                        filesLeft.Remove(gamePath.Relative);
                    }
                    DeleteEmptyDirectories(installDir, packageInstallationState.Files);
                }
                catch
                {
                    error = true;
                    throw;
                }
                finally
                {
                    updatePackageState(packageName,
                        filesLeft.Count == 0 ?
                            null :
                            packageInstallationState with
                            {
                                Partial = error,
                                Files = filesLeft
                            }
                        );
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
        IReadOnlyDictionary<string, PackageInstallationState> currentState,
        IReadOnlyCollection<IInstaller> installers,
        string installDir,
        Action<string, PackageInstallationState?> updatePackageState,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        // Increase by one for uninstall step
        var progress = new PercentOfTotal(installers.Count + 1);

        var allInstalledFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (installers.Count > 0)
        {
            eventHandler.InstallStart();

            foreach (var installer in installers.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
            {
                eventHandler.ProgressUpdate(progress.IncrementDone());
                eventHandler.InstallCurrent(installer.PackageName);
                var backupStrategy = backupStrategyProvider.BackupStrategy(state: null, eventHandler);
                var shadowedBy = new HashSet<string>();
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
                            shadowedBy.Add(overridingPackageName);
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
                    updatePackageState(installer.PackageName,
                        packageInstalledFiles.IsEmpty
                            ? null
                            : new PackageInstallationState(
                                Time: timeProvider.GetUtcNow().DateTime,
                                FsHash: installer.PackageFsHash,
                                Partial: installer.Installed == IInstallation.State.PartiallyInstalled,
                                Dependencies: installer.PackageDependencies,
                                ShadowedBy: shadowedBy,
                                Files: packageInstalledFiles
                        ));
                }
            }

            eventHandler.InstallEnd();
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
    public interface IEventHandler : IProgress, IBackupEventHandler
    {
        void InstallNoPackages();
        void InstallStart();
        void InstallCurrent(string packageName);
        void InstallEnd();

        void UninstallNoPackages();
        void UninstallStart();
        void UninstallCurrent(string packageName);
        void UninstallEnd();
    }

    public interface IProgress
    {
        public void ProgressUpdate(IPercent? progress);
    }
}
