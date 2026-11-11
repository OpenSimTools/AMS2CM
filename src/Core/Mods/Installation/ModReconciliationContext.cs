using System.Collections.Immutable;
using Core.Games;
using Core.Mods.Installation.Installers;
using Core.Packages;
using Core.Packages.Installation;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation;

public class ModReconciliationContext<TEventHandler, TConfig>(
    IReadOnlyDictionary<string, PackageInstallationState> previousState,
    IGame game,
    ITempDir tempDir,
    IBootfilesNaming bootfilesNaming,
    TConfig config) : PackageReconciliationContextBase<TEventHandler>(previousState)
    where TEventHandler : ModReconciliationContext.IEventHandler
    where TConfig : ModInstaller.IConfig, BootfilesInstaller.IConfig
{
    public static IReconciliationContext<TEventHandler>.FactoryFunc Factory(
        IGame game,
        ITempDir tempDir,
        IBootfilesNaming bootfilesNaming,
        TConfig config) => previousState =>
        new ModReconciliationContext<TEventHandler, TConfig>(
            previousState, game, tempDir, bootfilesNaming, config);

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

        var planningState = CurrentState
            .SelectValues(s => s with
            {
                Dependencies = s.Dependencies
                    .Select(d => bootfilesNaming.IsBootfiles(d) ? bootfilesName : d)
                    .ToArray()
            });

        var ordinaryState = planningState.Where(p => p.Key != bootfilesName).ToDictionary();
        return [.. CreatePackageActions(modInstallers.ToArray(), ordinaryState, actionFactory), () =>
        {
            var state = CurrentState.GetValueOrDefault(bootfilesName);
            var currentDependents = CurrentState
                .Where(p => p.Key != bootfilesName && p.Value.Dependencies.Contains(bootfilesName))
                .Select(p => p.Key).ToHashSet();
            if (currentDependents.Count == 0)
            {
                eventHandler.PostProcessingNotRequired();
                if (state is not null)
                {
                    ExecuteAction(actionFactory.Uninstall(bootfilesName, state));
                }
                return;
            }

            var bootfiles = CreateBootfilesInstaller(bootfilesPackage, CurrentState, eventHandler);
            if (state is not null)
            {
                ExecuteAction(actionFactory.Uninstall(bootfiles.PackageName, state));
            }
            ExecuteAction(actionFactory.Install(bootfiles));
        }];
    }

    protected virtual IPackageInstaller CreateModInstaller(IPackageInstaller packageInstaller,
        string bootfilesPackageName) =>
        new ModInstaller(packageInstaller, tempDir.BaseDirectory, config, game.InstallationDirectory,
            bootfilesPackageName);

    protected virtual IPackageInstaller CreateBootfilesInstaller(IPackage? bootfilesPackage,
        IReadOnlyDictionary<string, PackageInstallationState> state, TEventHandler eventHandler) =>
        new BootfilesInstaller(bootfilesPackage?.CreateInstaller(), tempDir.BaseDirectory, config,
            game.InstallationDirectory, bootfilesNaming, eventHandler);
}

public static class ModReconciliationContext
{
    public interface IEventHandler : IReconciliationContext.IEventHandler, BootfilesInstaller.IEventHandler
    {
    }
}
