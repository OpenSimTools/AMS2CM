using AMS2CM.CLI;
using Core.API;

try
{
    var config = Config.Load(args);
    var modManager = Init.CreateModManager(config);
    modManager.InstallEnabledMods(new ConsoleEventLogger());
}
catch (Exception e)
{
    Console.WriteLine($"Error: {e.Message}");
}

Console.WriteLine("Press any key to exit.");
Console.ReadKey();
