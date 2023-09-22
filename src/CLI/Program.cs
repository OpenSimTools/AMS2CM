using Core;

try
{
    var config = Config.Load(args);
    var modManager = Init.CreateModManager(config);
    modManager.Logs += Console.WriteLine;
    modManager.InstallEnabledMods();
}
catch (Exception e)
{
    Console.WriteLine($"Error: {e.Message}");
}

Console.WriteLine("Press any key to exit.");
Console.ReadKey();
