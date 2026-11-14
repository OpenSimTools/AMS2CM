using System.Collections.Immutable;
using System.IO.Abstractions;
using Core.Games;
using Core.Mods.Installation.Installers;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation;

public class ModReconciliationContext<TEventHandler, TConfig>(
    IReadOnlyDictionary<string, PackageInstallationState> previousState,
    IGame game,
    ITempDir tempDir,
    IBootfilesNaming bootfilesNaming,
    TConfig config,
    MoveFileBackupStrategy.IBackupFileNaming backupFileNaming,
    IFileUpdateTracker updateTracker) : PackageReconciliationContextBase<TEventHandler>(previousState)
    where TEventHandler : ModReconciliationContext.IEventHandler
    where TConfig : ModInstaller.IConfig, BootfilesInstaller.IConfig
{
    public static IReconciliationContext<TEventHandler>.FactoryFunc Factory(
        IGame game,
        ITempDir tempDir,
        IBootfilesNaming bootfilesNaming,
        TConfig config,
        MoveFileBackupStrategy.IBackupFileNaming backupFileNaming,
        IFileUpdateTracker updateTracker) => previousState =>
        new ModReconciliationContext<TEventHandler, TConfig>(
            previousState, game, tempDir, bootfilesNaming, config, backupFileNaming, updateTracker);

    private protected override IReadOnlyCollection<Action> CreateActions(IEnumerable<IPackage> packages,
        ReconciliationAction<TEventHandler>.Factory actionFactory,
        TEventHandler eventHandler)
    {
        var (bootfilesPackages, notBootfiles) = packages.ToImmutableArray()
            .Partition(p => bootfilesNaming.IsBootfiles(p.Name));
        var bootfilesPackage = bootfilesPackages.FirstOrDefault();
        var bootfilesName = bootfilesPackage?.Name ?? bootfilesNaming.GeneratedBootfilesName;
        var installers = notBootfiles.Select(p => p.CreateInstaller()).ToArray();
        var modInstallers = installers.Select(i => CreateModInstaller(i, bootfilesName));

        var planningState = CreatePlanningState(bootfilesName);
        var ordinaryState = planningState.Where(p => p.Key != bootfilesName).ToDictionary();
        return [.. CreatePackageActions(modInstallers.ToArray(), ordinaryState, actionFactory),
            () => ReconcileBootfiles(bootfilesPackage, bootfilesName, planningState, actionFactory, eventHandler)];
    }

    protected virtual IPackageInstaller CreateModInstaller(IPackageInstaller packageInstaller,
        string bootfilesPackageName) =>
        new ModInstaller(packageInstaller, tempDir.BaseDirectory, config, game.InstallationDirectory,
            bootfilesPackageName);

    private IReadOnlyDictionary<string, PackageInstallationState> CreatePlanningState(string bootfilesName) =>
        CurrentState.SelectValues(s => s with
        {
            Dependencies = s.Dependencies
                .Select(d => bootfilesNaming.IsBootfiles(d) ? bootfilesName : d).ToArray()
        });

    private void ReconcileBootfiles(IPackage? package, string packageName,
        IReadOnlyDictionary<string, PackageInstallationState> planningState,
        ReconciliationAction<TEventHandler>.Factory actionFactory, TEventHandler eventHandler)
    {
        var state = CurrentState.GetValueOrDefault(packageName);
        var currentDependents = Dependents(CurrentState, packageName);
        if (currentDependents.Count == 0)
        {
            if (state is not null)
            {
                ExecuteAction(actionFactory.Uninstall(packageName, state));
            }
            return;
        }

        var bootfiles = CreateBootfilesInstaller(package, CurrentState, eventHandler);
        var requiresInstallation = RequiresInstallation(bootfiles, state, planningState, currentDependents);
        if (state is not null)
        {
            ExecuteAction(requiresInstallation
                ? actionFactory.Uninstall(packageName, state)
                : actionFactory.Keep(packageName, state));
        }
        if (requiresInstallation)
        {
            ExecuteAction(actionFactory.Install(bootfiles));
        }
    }

    private static HashSet<string> Dependents(IReadOnlyDictionary<string, PackageInstallationState> state,
        string bootfilesName) =>
    [
        .. state.Where(p => p.Key != bootfilesName && p.Value.Dependencies.Contains(bootfilesName))
            .Select(p => p.Key)
    ];

    protected virtual IPackageInstaller CreateBootfilesInstaller(IPackage? bootfilesPackage,
        IReadOnlyDictionary<string, PackageInstallationState> state, TEventHandler eventHandler) =>
        new BootfilesInstaller(bootfilesPackage?.CreateInstaller() ??
            new GeneratedBootfilesInstaller(bootfilesNaming.GeneratedBootfilesName,
                tempDir.BaseDirectory, relative => ResolveSource(relative, state)), tempDir.BaseDirectory, config,
            game.InstallationDirectory, bootfilesNaming, eventHandler);

    private IFileInfo ResolveSource(string relative, IReadOnlyDictionary<string, PackageInstallationState> state)
    {
        var directory = game.InstallationDirectory;
        var live = directory.FileSystem.FileInfo.New(Path.Combine(directory.FullName, relative));
        var owner = state.Values.FirstOrDefault(s => s.Files.Any(p =>
            StringComparer.OrdinalIgnoreCase.Equals(Paths.NormalizeDirectorySeparators(p), relative)));
        return owner is not null && !updateTracker.HasBeenUpdated(live, owner.Time)
            ? directory.FileSystem.FileInfo.New(backupFileNaming.ToBackup(live.FullName))
            : live;
    }

    private bool RequiresInstallation(IPackageInstaller installer, PackageInstallationState? state,
        IReadOnlyDictionary<string, PackageInstallationState> planningState, IReadOnlySet<string> currentDependents)
    {
        if (state is null) return true;
        var previousDependents = Dependents(planningState, installer.PackageName);
        var previous = planningState.GetValueOrDefault(installer.PackageName);
        return !previousDependents.SetEquals(currentDependents) ||
            previousDependents.Union(currentDependents).Any(ReplacedPackages.Contains) ||
            state.Partial || state.VersionHash is null || installer.PackageVersionHash is null ||
            state.VersionHash != installer.PackageVersionHash ||
            state.ShadowedBy.Any(ReplacedPackages.Contains) ||
            state.ShadowedBy.Any(p => !CurrentState.ContainsKey(p)) ||
            (previous is not null && !previous.ShadowedBy.ToHashSet().SetEquals(state.ShadowedBy));
    }

    protected override void BeforePackageChange(string packageName, PackageInstallationState? previousState)
    {
        MarkBootfilesDependenciesPartial(previousState);
        foreach (var (name, state) in CurrentState.ToArray())
        {
            if (bootfilesNaming.IsBootfiles(name) && state.ShadowedBy.Contains(packageName))
            {
                MarkPartial(name);
            }
        }
    }

    private void MarkBootfilesDependenciesPartial(PackageInstallationState? state)
    {
        if (state is null) return;
        foreach (var dependency in state.Dependencies.Where(bootfilesNaming.IsBootfiles))
        {
            MarkPartial(dependency);
        }
    }

    protected override void AfterPackageProcessing(string packageName, bool changing)
    {
        if (changing)
        {
            MarkBootfilesDependenciesPartial(CurrentState.GetValueOrDefault(packageName));
        }
    }

    protected override void AfterOwnershipTransfer(string previousOwner)
    {
        if (bootfilesNaming.IsBootfiles(previousOwner))
        {
            MarkPartial(previousOwner);
        }
    }
}

public static class ModReconciliationContext
{
    public interface IEventHandler : IReconciliationContext.IEventHandler, BootfilesInstaller.IEventHandler;
}
