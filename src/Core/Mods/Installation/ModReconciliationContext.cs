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
        ReconciliationAction<TEventHandler>.Factory actionFactory, TEventHandler eventHandler)
    {
        var (bootfiles, notBootfiles) = packages.Select(p => p.CreateInstaller()).ToImmutableArray()
            .Partition(p => bootfilesNaming.IsBootfiles(p.PackageName));
        var bootfilesInstaller = CreateBootfilesInstaller(bootfiles.FirstOrDefault(), eventHandler);

        var modInstallers = notBootfiles
            .Select(i => CreateModInstaller(i, bootfilesInstaller))
            .Append(bootfilesInstaller);

        var planningState = CurrentState
            .SelectValues(s => s with
            {
                Dependencies = s.Dependencies
                    .Select(d => bootfilesNaming.IsBootfiles(d) ? bootfilesInstaller.PackageName : d)
                    .ToArray()
            });

        return CreatePackageActions(modInstallers.ToArray(), planningState, actionFactory);
    }

    protected virtual IPackageInstaller CreateModInstaller(IPackageInstaller packageInstaller,
        IPackageInstaller bootfilesInstaller) =>
        new ModInstaller(packageInstaller, tempDir.BaseDirectory, config, game.InstallationDirectory,
            bootfilesInstaller.PackageName);

    protected virtual IPackageInstaller CreateBootfilesInstaller(IPackageInstaller? bootfilesPackageInstaller,
        TEventHandler eventHandler) =>
        new BootfilesInstaller(bootfilesPackageInstaller, tempDir.BaseDirectory, config,
            game.InstallationDirectory, bootfilesNaming, eventHandler);
}

public static class ModReconciliationContext
{
    public interface IEventHandler : IReconciliationContext.IEventHandler, BootfilesInstaller.IEventHandler
    {
    }
}
