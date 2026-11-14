using System.IO.Abstractions;
using Core.Games;
using Core.IO;
using Core.Mods;
using Core.Mods.Installation;
using Core.Packages.Installation;
using Core.Packages.Installation.Backup;
using Core.Packages.Repository;
using Core.State;
using Core.State.JsonFile;
using Core.Utils;

namespace Core.API;

public static class Init
{
    private const string ModsDirName = "Mods";

    public static IModManager CreateModManager(Config config)
    {
        var fs = new FileSystem();
        var game = new Game(fs, config.Game);
        var modsDir = fs.DirectoryInfo.New(Path.Combine(game.InstallationDirectory.FullName, ModsDirName));
        var modRepository = new FileSystemRepository(modsDir);
        var statePersistence = new JsonFileStatePersistence(modsDir);
        var safeFileDelete = new WindowsRecyclingBin();
        var tempDir = new SubdirectoryTempDir(modsDir);
        return CreateModManager(fs, game, modRepository, statePersistence, safeFileDelete, tempDir, config.ModInstall);
    }

    public static IModManager CreateModManager(IFileSystem fs, IGame game,
        IPackageRepository modRepository,
        IStatePersistence statePersistence,
        ISafeFileDelete safeFileDelete,
        ITempDir tempDir,
        ModInstallConfig modInstallConfig)
    {
        var updateTracker = new CreationTimeFileUpdateTracker();
        var backupStrategyProvider = new SkipUpdatedBackupStrategy.Provider<IEventHandler>(
            fs, updateTracker,
            new SuffixBackupStrategy.Provider<DateTimeOffset, IEventHandler>(fs));
        var bootfilesNaming = new PrefixBootfilesNaming(modInstallConfig);
        var modReconciliationService = new ReconciliationService<IEventHandler>(
            backupStrategyProvider,
            ModReconciliationContext<IEventHandler, ModInstallConfig>.Factory(game, tempDir,
                bootfilesNaming, modInstallConfig, SuffixBackupStrategy.Naming, updateTracker));
        return new ModManager(game, modRepository, bootfilesNaming, modReconciliationService, statePersistence,
            safeFileDelete, tempDir);
    }
}
