using System.Collections.Immutable;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Packages.Installation;

public class PackageReconciliationService<TEventHandler>(
    IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider)
    : IReconciliationService<TEventHandler>
    where TEventHandler : PackageReconciliationService.IEventHandler
{
    public void Reconcile(
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
                    (state.ShadowedBy.Count > 0 && !state.ShadowedBy.Any(processed.Contains))))
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
        var allInstallers = uninstallers.Concat(installers).ToImmutableArray();
        if (allInstallers.IsEmpty)
        {
            eventHandler.UpdateNoPackages();
            return;
        }

        eventHandler.UpdateStart();

        var progress = new PercentOfTotal(allInstallers.Length);
        var installedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var installer in allInstallers.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
            eventHandler.UpdateCurrent(installer.PackageName);
            var backupStrategy = backupStrategyProvider.BackupStrategy(installer.InstallTime, eventHandler);
            var shadowedBy = new HashSet<string>();
            var installCallbacks = new ProcessingCallbacks<RootedPath>
            {
                Accept = gamePath =>
                {
                    var overridingPackageName = installedFiles.GetValueOrDefault(gamePath.Relative);
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
                Before = gamePath => installedFiles.Add(gamePath.Relative, installer.PackageName)
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

    private static IInstaller.Destination InstallTo(string destDir) =>
        relativePath => new RootedPath(destDir, relativePath);
}

public static class PackageReconciliationService
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
