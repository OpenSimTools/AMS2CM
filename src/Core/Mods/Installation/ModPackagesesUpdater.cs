using System.Collections.Immutable;
using Core.Games;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation;

public class ModPackagesesUpdater : PackagesUpdater<ModPackagesesUpdater.IEventHandler>
{
    #region TODO Move to a better place when not called all over the place

    public const string BootfilesPrefix = "__bootfiles";

    internal static bool IsBootFiles(string packageName) =>
        packageName.StartsWith(BootfilesPrefix);

    #endregion

    public new interface IEventHandler : PackagesUpdater.IEventHandler, BootfilesInstaller.IEventHandler
    {
    }

    private readonly IGame game;
    private readonly ITempDir tempDir;
    private readonly ModInstaller.IConfig config;

    public ModPackagesesUpdater(
        IInstallerFactory installerFactory,
        IBackupStrategyProvider<PackageInstallationState> backupStrategyProvider,
        TimeProvider timeProvider,
        IGame game,
        ITempDir tempDir,
        ModInstaller.IConfig config) :
        base(installerFactory, backupStrategyProvider, timeProvider)
    {
        this.game = game;
        this.tempDir = tempDir;
        this.config = config;
    }

    protected override void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> currentState,
        IReadOnlyCollection<IInstaller> installers,
        string installDir,
        Action<string, PackageInstallationState?> afterInstall,
        IEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var (bootfiles, notBootfiles) = installers.Partition(p => IsBootFiles(p.PackageName));

        var allInstallers = notBootfiles
            .Select(i => new ModInstaller(i, game, tempDir, config))
            .Append(CreateBootfilesInstaller(bootfiles, eventHandler)).ToImmutableArray();

        base.Apply(currentState, allInstallers, installDir, afterInstall, eventHandler, cancellationToken);
    }

    private IInstaller CreateBootfilesInstaller(IEnumerable<IInstaller> bootfilesPackageInstallers, IEventHandler eventHandler)
    {
        var installer = bootfilesPackageInstallers.FirstOrDefault();
        return new BootfilesInstaller(installer, game, tempDir, eventHandler, config);
    }
}
