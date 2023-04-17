﻿using Core;

try
{
    ModManager.Init().InstallEnabledMods();
}
catch (Exception e)
{
    Console.WriteLine($"Error: {e.Message}");
}
Console.WriteLine("Press any key to exit.");
Console.ReadKey();