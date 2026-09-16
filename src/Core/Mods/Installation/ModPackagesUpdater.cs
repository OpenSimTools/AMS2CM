using System.Collections.Immutable;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation;

public class ModPackagesUpdater<TEventHandler> : PackagesUpdater<TEventHandler>
    where TEventHandler : PackagesUpdater.IEventHandler
{
    private readonly IBootfilesNaming bootfilesNaming;
    private readonly IModInstallerFactory<TEventHandler> modInstallerFactory;

    public ModPackagesUpdater(
        IBackupStrategyProvider<PackageInstallationState, TEventHandler> backupStrategyProvider,
        TimeProvider timeProvider,
        IBootfilesNaming bootfilesNaming,
        IModInstallerFactory<TEventHandler> modInstallerFactory) :
        base(backupStrategyProvider, timeProvider)
    {
        this.bootfilesNaming = bootfilesNaming;
        this.modInstallerFactory = modInstallerFactory;
    }

    protected override void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> currentState,
        IReadOnlyCollection<IPackageInstaller> installers,
        string installDir,
        Action<string, PackageInstallationState?> updatePackageState,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var (bootfiles, notBootfiles) = installers.Partition(p => bootfilesNaming.IsBootfiles(p.PackageName));
        var bootfilesInstaller = CreateBootfilesInstaller(bootfiles, eventHandler);

        var allInstallers = notBootfiles
            .Select(i => modInstallerFactory.ModInstaller(i, bootfilesInstaller))
            .Append(bootfilesInstaller).ToImmutableArray();

        base.Apply(currentState, allInstallers, installDir, updatePackageState, eventHandler, cancellationToken);
    }

    private IPackageInstaller CreateBootfilesInstaller(IEnumerable<IPackageInstaller> bootfilesPackageInstallers, TEventHandler eventHandler)
    {
        var installer = bootfilesPackageInstallers.FirstOrDefault();
        return modInstallerFactory.BootfilesInstaller(installer, eventHandler);
    }
}
