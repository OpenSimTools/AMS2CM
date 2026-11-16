using AMS2CM.CLI;
using Core.API;

using var cancellation = new ConsoleCancellation(() =>
    Console.WriteLine("Cancelling installation...")
);

try
{
    var config = Config.Load(args);
    var modManager = Init.CreateModManager(config);
    modManager.InstallEnabledMods(new ConsoleEventLogger(), cancellation.Token);
}
catch (Exception e)
{
    Console.WriteLine($"Error: {e.Message}");
    return 1;
}

return cancellation.Token.IsCancellationRequested ? 130 : 0;
