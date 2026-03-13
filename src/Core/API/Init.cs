using Core.Games;
using Core.IO;
using Core.Mods;
using Core.Mods.Installation;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Repository;
using Core.State;
using Core.Utils;

namespace Core.API;

public static class Init
{
    private const string ModsDirName = "Mods";

    public static IModManager CreateModManager(Config config)
    {
        var game = new Game(config.Game);
        var modsDir = Path.Combine(game.InstallationDirectory, ModsDirName);
        var modRepository = new FileSystemRepository(modsDir);
        var statePersistence = new JsonFileStatePersistence(modsDir);
        var safeFileDelete = new WindowsRecyclingBin();
        var tempDir = new SubdirectoryTempDir(modsDir);
        return CreateModManager(game, modRepository, statePersistence, safeFileDelete, tempDir, config.ModInstall);
    }

    public static IModManager CreateModManager(
        IGame game,
        IPackageRepository modRepository,
        IStatePersistence statePersistence,
        ISafeFileDelete safeFileDelete,
        ITempDir tempDir,
        ModInstallConfig modInstallConfig)
    {
        var backupStrategyProvider = new SkipUpdatedBackupStrategy.Provider<IEventHandler>(
            new SuffixBackupStrategy.Provider<PackageInstallationState, IEventHandler>());
        var bootfilesNaming = new PrefixBootfilesNaming(modInstallConfig);
        var modInstallerFactory = new ModInstallerFactory<ModInstallConfig>(game, tempDir, bootfilesNaming, modInstallConfig);
        var modPackagesUpdater = new ModPackagesUpdater<IEventHandler>(
            new FileSystemInstallerFactory(), backupStrategyProvider,
            TimeProvider.System, bootfilesNaming, modInstallerFactory);
        return new ModManager(game, modRepository, bootfilesNaming, modPackagesUpdater, statePersistence, safeFileDelete, tempDir);
    }
}
