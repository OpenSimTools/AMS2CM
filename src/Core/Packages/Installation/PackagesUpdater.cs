using System.Collections.Immutable;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Packages.Installation;

public class PackagesUpdater<TEventHandler> : IPackagesUpdater<TEventHandler>
    where TEventHandler : PackagesUpdater.IEventHandler
{
    private readonly IBackupStrategyProvider<IHasTime, TEventHandler> backupStrategyProvider;
    private readonly TimeProvider timeProvider;

    public PackagesUpdater(
        IBackupStrategyProvider<IHasTime, TEventHandler>  backupStrategyProvider,
        TimeProvider timeProvider)
    {
        this.backupStrategyProvider = backupStrategyProvider;
        this.timeProvider = timeProvider;
    }

    public void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> previousState,
        IEnumerable<IPackage> packages,
        string installDir,
        Action<IReadOnlyDictionary<string, PackageInstallationState>> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var uninstallers = previousState
            .Select(entry =>
            {
                var (packageName, state) = entry;
                var backupStrategy = backupStrategyProvider.BackupStrategy(state, eventHandler);
                return new Uninstaller(packageName, state, installDir, backupStrategy);
            }).ToImmutableArray();
        var installers = packages.Select(package => package.Installer).ToImmutableArray();

        var currentState = new Dictionary<string, PackageInstallationState>(previousState);
        try
        {
            Apply(
                uninstallers,
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
        IReadOnlyCollection<IPackageInstaller> uninstallers,
        IReadOnlyCollection<IPackageInstaller> installers,
        string installDir,
        Action<string, PackageInstallationState?> updatePackageState,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        UninstallPackages(uninstallers, installDir, updatePackageState, eventHandler, cancellationToken);
        InstallPackages(installers, installDir, updatePackageState, eventHandler, cancellationToken);
    }

    private void UninstallPackages(
        IReadOnlyCollection<IPackageInstaller> uninstallers,
        string installDir,
        Action<string, PackageInstallationState?> updatePackageState,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        if (uninstallers.Count > 0)
        {
            eventHandler.UninstallStart();
            foreach (var uninstaller in uninstallers)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                eventHandler.UninstallCurrent(uninstaller.PackageName);
                var backupStrategy = backupStrategyProvider.BackupStrategy(null, eventHandler);
                try
                {
                    uninstaller.Install(InstallTo(installDir), backupStrategy, new ProcessingCallbacks<RootedPath>());
                }
                finally
                {
                    var packageInstalledFiles = uninstaller.InstalledFiles
                        .Where(rp => rp.Root == installDir)
                        .Select(rp => rp.Relative)
                        .ToImmutableList();
                    updatePackageState(uninstaller.PackageName,
                        packageInstalledFiles.IsEmpty
                            ? null
                            : new PackageInstallationState(
                                Time: uninstaller.InstallTime,
                                VersionHash: uninstaller.PackageVersionHash,
                                Partial: uninstaller.Installed == IInstallation.State.PartiallyInstalled,
                                Dependencies: uninstaller.PackageDependencies,
                                ShadowedBy: Array.Empty<string>(), // It doesn't matter when partially installed
                                Files: packageInstalledFiles
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

    private void InstallPackages(
        IReadOnlyCollection<IPackageInstaller> installers,
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
                                VersionHash: installer.PackageVersionHash,
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
