using System.Collections.Immutable;
using System.IO.Abstractions;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Packages.Installation;

public abstract class PackageReconciliationContextBase<TEventHandler>(
    IReadOnlyDictionary<string, PackageInstallationState> currentState) : IReconciliationContext<TEventHandler>
    where TEventHandler : IReconciliationContext.IEventHandler
{
    private readonly Dictionary<string, PackageInstallationState> currentState = new(currentState);
    private readonly Dictionary<RootedPath, string> installedFiles = [];

    public IReadOnlyDictionary<string, PackageInstallationState> CurrentState => currentState;

    public IReadOnlyCollection<Action> ReconciliationActions(IEnumerable<IPackage> packages,
        IDirectoryInfo installDir,
        IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider,
        TEventHandler eventHandler)
    {
        var actionFactory = CreateActionFactory(backupStrategyProvider, installDir, eventHandler);
        return CreateActions(packages, actionFactory, eventHandler);
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

    private protected abstract IReadOnlyCollection<Action> CreateActions(IEnumerable<IPackage> packages,
        ReconciliationAction<TEventHandler>.Factory actionFactory,
        TEventHandler eventHandler);

    private protected IReadOnlyCollection<Action> CreatePackageActions(
        IReadOnlyCollection<IPackageInstaller> installers,
        IReadOnlyDictionary<string, PackageInstallationState> planningState,
        ReconciliationAction<TEventHandler>.Factory actionFactory)
    {
        var joinedState = JoinPreviousState(planningState, installers);
        HashSet<string> processed = [];
        HashSet<string> toUninstall = [];
        List<Action> actions = [];
        foreach (var (packageName, (state, installer)) in joinedState)
        {
            processed.Add(packageName);
            var uninstall = state is not null && ShouldUninstall(state, installer, toUninstall, processed);
            if (uninstall)
            {
                toUninstall.Add(packageName);
            }

            // Decide replacements from the original plan, but resolve ownership after earlier actions.
            actions.Add(() => ProcessPackage(packageName, state, installer, uninstall, actionFactory));
        }
        return actions;
    }

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

    private static bool ShouldUninstall(PackageInstallationState state, IPackageInstaller? installer,
        IReadOnlySet<string> toUninstall, IReadOnlySet<string> processed) =>
        state.Partial ||
        installer is null ||
        state.VersionHash is null ||
        installer.PackageVersionHash is null ||
        state.VersionHash != installer.PackageVersionHash ||
        state.ShadowedBy.Intersect(toUninstall).Any() ||
        (state.ShadowedBy.Count > 0 && !state.ShadowedBy.Any(processed.Contains));

    private void ProcessPackage(string packageName, PackageInstallationState? state,
        IPackageInstaller? installer, bool uninstall, ReconciliationAction<TEventHandler>.Factory actionFactory)
    {
        foreach (var action in ResolveActions(packageName, state, installer, uninstall, actionFactory))
        {
            ExecuteAction(action);
        }
    }

    private List<IReconciliationAction> ResolveActions(
        string packageName, PackageInstallationState? state, IPackageInstaller? installer, bool uninstall,
        ReconciliationAction<TEventHandler>.Factory actionFactory)
    {
        if (state is not null)
        {
            var ownership = currentState[packageName];
            // Keep planning metadata, including remapped bootfiles dependencies.
            state = state with { Files = ownership.Files, ShadowedBy = ownership.ShadowedBy };
        }
        List<IReconciliationAction> actions = [];
        if (uninstall && state is not null)
        {
            actions.Add(actionFactory.Uninstall(packageName, state));
        }
        if (installer is null) return actions;
        if (state is null || uninstall)
        {
            actions.Add(actionFactory.Install(installer));
        }
        else
        {
            actions.Add(actionFactory.Keep(packageName, state));
        }
        return actions;
    }

    protected void ExecuteAction(IReconciliationAction action)
    {
        var shadowedBy = new HashSet<string>(action.ShadowedBy);
        var installCallbacks = new ProcessingCallbacks<RootedPath>
        {
            Accept = gamePath => AcceptFile(gamePath, action.PackageName, shadowedBy),
            Before = gamePath => ClaimFile(gamePath, action.PackageName)
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

    private bool AcceptFile(RootedPath gamePath, string packageName, HashSet<string> shadowedBy)
    {
        var shadowingPackageName = installedFiles.GetValueOrDefault(gamePath);
        if (shadowingPackageName is null) return true;
        if (shadowingPackageName != packageName)
        {
            shadowedBy.Add(shadowingPackageName);
        }
        return false;
    }

    private void ClaimFile(RootedPath gamePath, string packageName)
    {
        installedFiles[gamePath] = packageName;
        foreach (var (name, state) in currentState.ToArray())
        {
            if (name == packageName || !state.Files.Any(p => gamePath.Root.Combine(p) == gamePath)) continue;
            currentState[name] = state with
            {
                Files = state.Files.Where(p => gamePath.Root.Combine(p) != gamePath).ToArray(),
                ShadowedBy = state.ShadowedBy.Append(packageName).Distinct().ToArray()
            };
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
