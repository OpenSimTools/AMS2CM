using AMS2CM;

try
{
    ModManager.Init().InstallEnabledMods();
}
catch (Exception e)
{
    Console.WriteLine(e.Message);
}
Console.WriteLine("Press any key to exit.");
Console.ReadKey();