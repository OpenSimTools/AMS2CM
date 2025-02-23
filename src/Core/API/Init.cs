using Core.Backup;
using Core.Bootfiles;
using Core.Games;
using Core.IO;
using Core.Mods;
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
        var modRepository = new ModRepository(modsDir);
        var safeFileDelete = new WindowsRecyclingBin();
        var modPackagesUpdater = CreateModPackagesUpdater(config.ModInstall, game, tempDir);
        return new ModManager(game, modRepository, modPackagesUpdater, statePersistence, safeFileDelete, tempDir);
    }

    internal static ModPackagesUpdater<IEventHandler> CreateModPackagesUpdater(
        BaseInstaller.IConfig installerConfig,
        IGame game,
        ITempDir tempDir)
    {
        var backupStrategy = new SuffixBackupStrategy();
        var installationsUpdater = new InstallationsUpdater(backupStrategy);
        var bootfilesAwareUpdater = new BootfilesAwareUpdater<IEventHandler>(installationsUpdater, game, tempDir, installerConfig);
        return new ModPackagesUpdater<IEventHandler>(bootfilesAwareUpdater, tempDir, installerConfig);
    }
}
