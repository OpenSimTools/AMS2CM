using Core.Games;
using Core.IO;
using Core.Mods.Installation;
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
        var tempDir = new SubdirectoryTempDir(modsDir);
        var statePersistence = new JsonFileStatePersistence(modsDir);
        var modRepository = new FileSystemRepository(modsDir);
        var safeFileDelete = new WindowsRecyclingBin();
        var modPackagesUpdater = CreateModPackagesUpdater(config.ModInstall, game, tempDir);
        return new ModManager(game, modRepository, modPackagesUpdater, statePersistence, safeFileDelete, tempDir);
    }

    internal static PackagesUpdater<IEventHandler> CreateModPackagesUpdater(
        ModInstallConfig installerConfig,
        IGame game,
        ITempDir tempDir)
    {
        var backupStrategy = new SuffixBackupStrategy();
        var backupStrategyProvider = new SkipUpdatedBackupStrategy.Provider(backupStrategy);
        var installationsUpdater = new InstallationsUpdater(backupStrategyProvider, TimeProvider.System);
        var bootfilesAwareUpdater = new ModInstallationsUpdater<IEventHandler>(installationsUpdater, game, tempDir, installerConfig);
        return new PackagesUpdater<IEventHandler>(bootfilesAwareUpdater);
    }
}
