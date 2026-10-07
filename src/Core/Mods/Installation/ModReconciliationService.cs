using System.Collections.Immutable;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation;

public class ModReconciliationService<TEventHandler>(
    IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider,
    IBootfilesNaming bootfilesNaming,
    IModInstallerFactory<TEventHandler> modInstallerFactory)
    : PackageReconciliationService<TEventHandler>(backupStrategyProvider)
    where TEventHandler : PackageReconciliationService.IEventHandler
{
    protected override IReadOnlyCollection<IReconciliationAction> ReconciliationActions(
        IReadOnlyDictionary<string, PackageInstallationState> previousState,
        IEnumerable<IPackageInstaller> installers, string installDir, TEventHandler eventHandler)
    {
        var (bootfiles, notBootfiles) = installers.ToImmutableArray()
            .Partition(p => bootfilesNaming.IsBootfiles(p.PackageName));
        var bootfilesInstaller = CreateBootfilesInstaller(bootfiles, eventHandler);

        var modInstallers = notBootfiles
            .Select(i => modInstallerFactory.ModInstaller(i, bootfilesInstaller))
            .Append(bootfilesInstaller).ToImmutableArray();

        var stateWithBootfilesDepsReplaced = previousState
            .SelectValues(s => s with
            {
                Dependencies = s.Dependencies
                    .Select(d => bootfilesNaming.IsBootfiles(d) ? bootfilesInstaller.PackageName : d)
                    .ToArray()
            });

        return base.ReconciliationActions(stateWithBootfilesDepsReplaced, modInstallers, installDir, eventHandler);
    }

    private IPackageInstaller CreateBootfilesInstaller(IEnumerable<IPackageInstaller> bootfilesPackageInstallers, TEventHandler eventHandler)
    {
        var installer = bootfilesPackageInstallers.FirstOrDefault();
        return modInstallerFactory.BootfilesInstaller(installer, eventHandler);
    }
}
