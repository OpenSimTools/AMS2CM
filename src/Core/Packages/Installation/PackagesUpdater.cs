using System.Collections.Immutable;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Packages.Installation;

public class PackagesUpdater<TEventHandler> : IPackagesUpdater<TEventHandler>
    where TEventHandler : PackagesUpdater.IEventHandler
{
    private readonly IBackupStrategyProvider<DateTime, TEventHandler> backupStrategyProvider;
    private readonly TimeProvider timeProvider;

    public PackagesUpdater(
        IBackupStrategyProvider<DateTime, TEventHandler>  backupStrategyProvider,
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
                var backupStrategy = backupStrategyProvider.BackupStrategy(state.Time, eventHandler);
                return new Uninstaller(packageName, state, installDir, backupStrategy);
            });
        var installers = packages.Select(package => package.Installer);

        var currentState = new Dictionary<string, PackageInstallationState>(previousState);
        try
        {
            Apply(
                uninstallers.Concat(installers).ToImmutableArray(),
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
        IReadOnlyCollection<IPackageInstaller> installers,
        string installDir,
        Action<string, PackageInstallationState?> updatePackageState,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var progress = new PercentOfTotal(installers.Count);

        var allInstalledFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (installers.Count > 0)
        {
            eventHandler.UpdateStart();

            foreach (var installer in installers.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
            {
                eventHandler.UpdateCurrent(installer.PackageName);
                var backupStrategy = backupStrategyProvider.BackupStrategy(timeProvider.GetUtcNow().DateTime, eventHandler);
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
                                Time: installer.InstallTime,
                                VersionHash: installer.PackageVersionHash,
                                Partial: installer.Installed == IInstallation.State.PartiallyInstalled,
                                Dependencies: installer.PackageDependencies,
                                ShadowedBy: shadowedBy,
                                Files: packageInstalledFiles
                        ));
                }
                eventHandler.ProgressUpdate(progress.IncrementDone());
            }

            eventHandler.UpdateEnd();
        }
        else
        {
            eventHandler.UpdateNoPackages();
        }
    }

    private static IInstaller.Destination InstallTo(string destDir) =>
        relativePath => new RootedPath(destDir, relativePath);
}

public static class PackagesUpdater
{
    public interface IEventHandler : IProgress, IBackupEventHandler
    {
        void UpdateNoPackages();
        void UpdateStart();
        void UpdateCurrent(string packageName);
        void UpdateEnd();
    }

    public interface IProgress
    {
        public void ProgressUpdate(IPercent? progress);
    }
}
