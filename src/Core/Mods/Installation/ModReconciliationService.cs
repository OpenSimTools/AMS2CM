using System.Collections.Immutable;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation;

public class ModReconciliationService<TEventHandler> : PackageReconciliationService<TEventHandler>
    where TEventHandler : PackageReconciliationService.IEventHandler
{
    private readonly IBootfilesNaming bootfilesNaming;
    private readonly IModInstallerFactory<TEventHandler> modInstallerFactory;

    public ModReconciliationService(
        IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider,
        TimeProvider _,
        IBootfilesNaming bootfilesNaming,
        IModInstallerFactory<TEventHandler> modInstallerFactory) :
        base(backupStrategyProvider)
    {
        this.bootfilesNaming = bootfilesNaming;
        this.modInstallerFactory = modInstallerFactory;
    }

    protected override void Apply(
        IReadOnlyCollection<IPackageInstaller> uninstallers,
        IReadOnlyCollection<IPackageInstaller> installers,
        string installDir,
        Action<string, PackageInstallationState?> updatePackageState,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var (bootfiles, notBootfiles) = installers.Partition(p => bootfilesNaming.IsBootfiles(p.PackageName));
        var bootfilesInstaller = CreateBootfilesInstaller(bootfiles, eventHandler);

        var modInstallers = notBootfiles
            .Select(i => modInstallerFactory.ModInstaller(i, bootfilesInstaller))
            .Append(bootfilesInstaller).ToImmutableArray();

        base.Apply(uninstallers, modInstallers, installDir, updatePackageState, eventHandler, cancellationToken);
    }

    private IPackageInstaller CreateBootfilesInstaller(IEnumerable<IPackageInstaller> bootfilesPackageInstallers, TEventHandler eventHandler)
    {
        var installer = bootfilesPackageInstallers.FirstOrDefault();
        return modInstallerFactory.BootfilesInstaller(installer, eventHandler);
    }
}
