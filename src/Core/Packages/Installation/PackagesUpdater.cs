using System.Collections.Immutable;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Packages.Installation;

public class PackagesUpdater<TEventHandler>(
    IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider,
    TimeProvider timeProvider)
    : IPackagesUpdater<TEventHandler>
    where TEventHandler : PackagesUpdater.IEventHandler
{
    public void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> previousState,
        IReadOnlyCollection<IPackage> packages,
        string installDir,
        Action<IReadOnlyDictionary<string, PackageInstallationState>> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var joinedState = new OrderedDictionary<string, (PackageInstallationState?, IPackage?)>();
        var available = packages.Select(p => p.Name).ToImmutableHashSet();
        foreach (var (packageName, state) in previousState)
        {
            if (!available.Contains(packageName))
            {
                joinedState.Add(packageName, (state, null));
            }
        }
        foreach (var p in packages)
        {
            joinedState.Add(p.Name, (previousState.GetValueOrDefault(p.Name), p));
        }

        var uninstallers = new List<IPackageInstaller>();
        var installers = new List<IPackageInstaller>();
        var toUninstall = new HashSet<string>();
        var processed = new HashSet<string>();
        var rif = new ReplacementInstaller.Factory<TEventHandler>(installDir, backupStrategyProvider, eventHandler);
        foreach (var (packageName, (state, package)) in joinedState)
        {
            processed.Add(packageName);

            if (state is not null && (
                    state.Partial ||
                    package is null ||
                    state.VersionHash != package.VersionHash ||
                    state.ShadowedBy.Intersect(toUninstall).Any() ||
                    !state.ShadowedBy.Intersect(processed).Any()))
            {
                uninstallers.Add(rif.Uninstall(packageName, state));
                toUninstall.Add(packageName);
            }

            if (package is null)
            {
                continue;
            }
            if (state is null || toUninstall.Contains(packageName))
            {
                installers.Add(package.Installer);
            }
            else
            {
                installers.Add(rif.Keep(packageName, state));
            }
        }

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
                var backupStrategy = backupStrategyProvider.BackupStrategy(installer.InstallTime, eventHandler);
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
