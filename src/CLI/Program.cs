using Core;
using Microsoft.Extensions.Configuration;

try
{
    var config = new ConfigurationBuilder()
        .AddIniFile("AMS2CM.ini")
        .AddCommandLine(args)
        .Build();
    var gameConfig = config
        .GetSection("Game")
        .Get<ModManager.GameConfig>() ?? throw new Exception("Failed to read the configuration file");
    var modManagerConfig = new ModManager.Config(Game: gameConfig);
    var modManager = ModManager.Init(modManagerConfig);
    modManager.InstallEnabledMods();
}
catch (Exception e)
{
    Console.WriteLine($"Error: {e.Message}");
}
Console.WriteLine("Press any key to exit.");
Console.ReadKey();
