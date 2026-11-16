using AMS2CM.CLI;
using Core;
using Core.API;
using Microsoft.Extensions.DependencyInjection;

using var cancellation = new ConsoleCancellation(() =>
    Console.WriteLine("Cancelling installation...")
);

try
{
    var config = Config.Load(args);
    var services = new ServiceCollection().AddCoreServices(config);
    using var provider = services.BuildServiceProvider(new ServiceProviderOptions
    {
        ValidateOnBuild = true,
        ValidateScopes = true
    });
    var modManager = provider.GetRequiredService<IModManager>();
    modManager.InstallEnabledMods(new ConsoleEventLogger(), cancellation.Token);
}
catch (Exception e)
{
    Console.WriteLine($"Error: {e.Message}");
    return 1;
}

return cancellation.Token.IsCancellationRequested ? 130 : 0;
