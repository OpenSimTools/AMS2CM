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
        IEnumerable<IPackage> packages,
        string installDir,
        Action<IReadOnlyDictionary<string, PackageInstallationState>> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var currentState = new Dictionary<string, PackageInstallationState>(previousState);
        var installers = packages.Select(p => p.Installer).ToArray();
        try
        {
            Execute(
                ReconciliationActions(previousState, installers, installDir, eventHandler),
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

    protected virtual IReadOnlyCollection<IReconciliationAction> ReconciliationActions(IReadOnlyDictionary<string, PackageInstallationState> previousState, IEnumerable<IPackageInstaller> installers, string installDir,
        TEventHandler eventHandler)
    {
        var installersCollection = installers.ToArray();
        var joinedState = new OrderedDictionary<string, (PackageInstallationState?, IPackageInstaller?)>();

        var available = installersCollection.Select(p => p.PackageName).ToImmutableHashSet();
        foreach (var (packageName, state) in previousState)
        {
            if (!available.Contains(packageName))
            {
                joinedState.Add(packageName, (state, null));
            }
        }

        foreach (var i in installersCollection)
        {
            joinedState.Add(i.PackageName, (previousState.GetValueOrDefault(i.PackageName), i));
        }

        var reconciliationActions = new List<IReconciliationAction>();
        var toUninstall = new HashSet<string>();
        var processed = new HashSet<string>();
        var raf = new ReconciliationAction<TEventHandler>.Factory(installDir, backupStrategyProvider, eventHandler);
        foreach (var (packageName, (state, installer)) in joinedState)
        {
            processed.Add(packageName);

            if (state is not null && (
                    state.Partial ||
                    installer is null ||
                    state.VersionHash is null ||
                    installer.PackageVersionHash is null ||
                    state.VersionHash != installer.PackageVersionHash ||
                    state.ShadowedBy.Intersect(toUninstall).Any() ||
                    (state.ShadowedBy.Count > 0 && !state.ShadowedBy.Any(processed.Contains))))
            {
                reconciliationActions.Add(raf.Uninstall(packageName, state));
                toUninstall.Add(packageName);
            }

            if (installer is null)
            {
                continue;
            }
            if (state is null || toUninstall.Contains(packageName))
            {
                reconciliationActions.Add(raf.Install(installer));
            }
            else
            {
                reconciliationActions.Add(raf.Keep(packageName, state));
            }
        }

        return reconciliationActions;
    }

    private static void Execute(
        IReadOnlyCollection<IReconciliationAction> reconciliationActions,
        Action<string, PackageInstallationState?> updatePackageState,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        if (reconciliationActions.Count == 0)
        {
            eventHandler.ReconciliationNoPackages();
            return;
        }

        eventHandler.ReconciliationStart();

        var progress = new PercentOfTotal(reconciliationActions.Count);
        var installedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in reconciliationActions.TakeWhile(_ => !cancellationToken.IsCancellationRequested))
        {
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
                    if (overridingPackageName != action.PackageName)
                    {
                        shadowedBy.Add(overridingPackageName);
                    }
                    return false;
                },
                Before = gamePath => installedFiles.Add(gamePath.Relative, action.PackageName)
            };
            try
            {
                action.Execute(installCallbacks);
            }
            finally
            {
                var files = action.InstalledFiles;
                updatePackageState(action.PackageName,
                    files.Count == 0
                        ? null
                        : new PackageInstallationState(
                            Time: action.InstallTime,
                            VersionHash: action.PackageVersionHash,
                            Partial: action.InstallState == IInstallation.State.PartiallyInstalled,
                            Dependencies: action.PackageDependencies,
                            ShadowedBy: shadowedBy,
                            Files: files
                    ));
            }
            eventHandler.ProgressUpdate(progress.IncrementDone());
        }

        eventHandler.ReconciliationEnd();
    }
}

public static class PackageReconciliationService
{
    public interface IEventHandler : IProgress, IBackupEventHandler
    {
        void ReconciliationNoPackages();
        void ReconciliationStart();
        void InstallingPackage(string packageName);
        void SkippingPackage(string packageName);
        void UninstallingPackage(string packageName);
        void ReconciliationEnd();
    }

    public interface IProgress
    {
        public void ProgressUpdate(IPercent? progress);
    }
}
