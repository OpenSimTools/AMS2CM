using Core.Games;
using Core.State;

namespace Core;

public static class Init
{
    private const string ModsDirName = "Mods";

    public static IModManager CreateModManager(string[] args)
    {
        var config = Config.Load(args);
        var game = new Game(config.Game);
        var modsDir = Path.Combine(game.InstallationDirectory, ModsDirName);
        var statePersistence = new JsonFileStatePersistence(modsDir);
        var modFactory = new ModFactory(config.ModInstall, game);
        return new ModManager(game, modsDir, modFactory, statePersistence);
    }
}
