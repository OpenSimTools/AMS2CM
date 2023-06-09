﻿using Core;

try
{
    var modManager = Init.CreateModManager(args, oldStateIsPrimary: true);
    modManager.Logs += Console.WriteLine;
    modManager.InstallEnabledMods();
}
catch (Exception e)
{
    Console.WriteLine($"Error: {e.Message}");
}

Console.WriteLine("Press any key to exit.");
Console.ReadKey();
