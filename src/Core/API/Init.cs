using System.IO.Abstractions;
using Core.Games;
using Core.IO;
using Core.Mods;
using Core.Mods.Installation;
using Core.Mods.Installation.Installers;
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
        var file = game.InstallationDirectory.FileSystem.File;
        var backupStrategyProvider = new SkipUpdatedBackupStrategy.Provider<IEventHandler>(
            file,
            new SuffixBackupStrategy.Provider<DateTimeOffset, IEventHandler>(file));
        var bootfilesNaming = new PrefixBootfilesNaming(modInstallConfig);
        var modInstallerFactory = new ModInstallerFactory<ModInstallConfig>(game, tempDir, bootfilesNaming,
            modInstallConfig);
        var modReconciliationService = new ModReconciliationService<IEventHandler>(backupStrategyProvider,
            bootfilesNaming, modInstallerFactory);
        return new ModManager(game, modRepository, bootfilesNaming, modReconciliationService, statePersistence,
            safeFileDelete, tempDir);
    }
}
