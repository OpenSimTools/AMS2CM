using Core;
using Core.Games;

try
{
    var config = Config.Load(args);
    var game = new Game(config.Game);
    var modFactory = new ModFactory(config.ModInstall, game);
    var modManager = new ModManager(game, modFactory);
    modManager.InstallEnabledMods();
}
catch (Exception e)
{
    Console.WriteLine($"Error: {e.Message}");
}
Console.WriteLine("Press any key to exit.");
Console.ReadKey();
