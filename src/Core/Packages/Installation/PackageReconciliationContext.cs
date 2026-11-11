using System.Collections.Immutable;
using System.IO.Abstractions;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Packages.Installation;

public class PackageReconciliationContext<TEventHandler>(
    IReadOnlyDictionary<string, PackageInstallationState> currentState) : IReconciliationContext<TEventHandler>
    where TEventHandler : IReconciliationContext.IEventHandler
{
    public static IReconciliationContext<TEventHandler>.FactoryFunc Factory() =>
        currentState => new PackageReconciliationContext<TEventHandler>(currentState);

    private readonly Dictionary<string, PackageInstallationState> currentState = new(currentState);
    private readonly Dictionary<RootedPath, string> installedFiles = [];

    public IReadOnlyDictionary<string, PackageInstallationState> CurrentState => currentState;

    protected sealed record PlanningInputs(
        IReadOnlyList<IPackageInstaller> Installers,
        IReadOnlyDictionary<string, PackageInstallationState> PlanningState);

    public IReadOnlyCollection<Action> ReconciliationActions(IEnumerable<IPackage> packages,
        IDirectoryInfo installDir,
        IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider,
        TEventHandler eventHandler)
    {
        var inputs = CreatePlanningInputs(packages, eventHandler);
        var joinedState = JoinPreviousState(inputs.PlanningState, inputs.Installers);
        var actionFactory = CreateActionFactory(backupStrategyProvider, installDir, eventHandler);
        return CreateActions(joinedState, actionFactory).Select(action => (Action)(() => ExecuteAction(action))).ToArray();
    }

    protected virtual PlanningInputs CreatePlanningInputs(IEnumerable<IPackage> packages,
        TEventHandler eventHandler) => new(packages.Select(p => p.CreateInstaller()).ToArray(), currentState);

    private static OrderedDictionary<string, (PackageInstallationState?, IPackageInstaller?)> JoinPreviousState(
        IReadOnlyDictionary<string, PackageInstallationState> planningState,
        IReadOnlyCollection<IPackageInstaller> installersCollection)
    {
        var joinedState = new OrderedDictionary<string, (PackageInstallationState?, IPackageInstaller?)>();
        var available = installersCollection.Select(p => p.PackageName).ToImmutableHashSet();
        foreach (var (packageName, state) in planningState)
        {
            if (!available.Contains(packageName))
            {
                joinedState.Add(packageName, (state, null));
            }
        }

        foreach (var i in installersCollection)
        {
            joinedState.Add(i.PackageName, (planningState.GetValueOrDefault(i.PackageName), i));
        }

        return joinedState;
    }

    private ReconciliationAction<TEventHandler>.Factory CreateActionFactory(
        IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider, IDirectoryInfo installDir,
        TEventHandler eventHandler)
    {
        var installRoot = new PathRoot(installDir);
        var previouslyInstalledPaths = currentState.Values
            .SelectMany(state => state.Files).Select(installRoot.Combine);
        var trackingBackupProvider = new SkipInstalledBackupStrategy.Provider<TEventHandler>(
            backupStrategyProvider, previouslyInstalledPaths);
        return new ReconciliationAction<TEventHandler>.Factory(installDir, trackingBackupProvider, eventHandler);
    }

    private static IReadOnlyCollection<IReconciliationAction> CreateActions(
        OrderedDictionary<string, (PackageInstallationState?, IPackageInstaller?)> joinedState,
        ReconciliationAction<TEventHandler>.Factory actionFactory)
    {
        HashSet<string> processed = [];
        HashSet<string> toUninstall = [];
        var uninstallActions = new List<IReconciliationAction>();
        var installOrKeepActions = new List<IReconciliationAction>();
        foreach (var (packageName, (state, installer)) in joinedState)
        {
            processed.Add(packageName);

            if (state is not null && ShouldUninstall(state, installer, toUninstall, processed))
            {
                uninstallActions.Add(actionFactory.Uninstall(packageName, state));
                toUninstall.Add(packageName);
            }

            if (installer is null)
            {
                continue;
            }

            if (state is null || toUninstall.Contains(packageName))
            {
                installOrKeepActions.Add(actionFactory.Install(installer));
            }
            else
            {
                installOrKeepActions.Add(actionFactory.Keep(packageName, state));
            }
        }

        return [.. uninstallActions, .. installOrKeepActions];
    }

    private static bool ShouldUninstall(PackageInstallationState state, IPackageInstaller? installer,
        IReadOnlySet<string> toUninstall, IReadOnlySet<string> processed) =>
        state.Partial ||
        installer is null ||
        state.VersionHash is null ||
        installer.PackageVersionHash is null ||
        state.VersionHash != installer.PackageVersionHash ||
        state.ShadowedBy.Intersect(toUninstall).Any() ||
        (state.ShadowedBy.Count > 0 && !state.ShadowedBy.Any(processed.Contains));

    private void ExecuteAction(IReconciliationAction action)
    {
        var shadowedBy = new HashSet<string>();
        var installCallbacks = new ProcessingCallbacks<RootedPath>
        {
            Accept = gamePath =>
            {
                var shadowingPackageName = installedFiles.GetValueOrDefault(gamePath);
                if (shadowingPackageName is null)
                {
                    return true;
                }

                if (shadowingPackageName != action.PackageName)
                {
                    shadowedBy.Add(shadowingPackageName);
                }

                return false;
            },
            Before = gamePath => installedFiles.Add(gamePath, action.PackageName)
        };
        try
        {
            action.Execute(installCallbacks);
        }
        finally
        {
            UpdateState(action, shadowedBy);
        }
    }

    private void UpdateState(IReconciliationAction action, IReadOnlyCollection<string> shadowedBy)
    {
        var files = action.InstalledFiles;
        var packageName = action.PackageName;
        if (files.Count == 0)
        {
            currentState.Remove(packageName);
        }
        else
        {
            currentState[packageName] = new PackageInstallationState(
                Time: action.InstallTime,
                VersionHash: action.PackageVersionHash,
                Partial: action.InstallState == IInstallation.State.PartiallyInstalled,
                Dependencies: action.PackageDependencies,
                ShadowedBy: shadowedBy,
                Files: files
            );
        }
    }
}
