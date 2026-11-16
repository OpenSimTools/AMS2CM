using Core;
using Core.API;
using Core.SoftwareUpdates;
using Microsoft.Extensions.DependencyInjection;

namespace AMS2CM.GUI;

public partial class App : Application
{
    private MainWindow? window;
    private ServiceProvider? serviceProvider;
    private WindowsAccentTheme? accentTheme;

    public App()
    {
        InitializeComponent();
        UnhandledException += HandleUnhandledException;
    }

    private void HandleUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        if (window is null)
        {
            return;
        }
        args.Handled = true;
        _ = window.SignalErrorAsync(args.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var config = Config.Load(Environment.GetCommandLineArgs());
        var services = new ServiceCollection().AddCoreServices(config);
        serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        try
        {
            accentTheme = new WindowsAccentTheme(Resources);
            window = new MainWindow(
                ResolveModManager(serviceProvider),
                serviceProvider.GetRequiredService<IUpdateChecker>());
            window.Closed += HandleWindowClosed;
            window.Activate();
        }
        catch
        {
            DisposeServices();
            throw;
        }
    }

    private static IModManager ResolveModManager(IServiceProvider provider)
    {
        try
        {
            return provider.GetRequiredService<IModManager>();
        }
        catch (Exception ex)
        {
            return new ThrowingModManager(ex);
        }
    }

    private void HandleWindowClosed(object sender, WindowEventArgs args) => DisposeServices();

    private void DisposeServices()
    {
        Interlocked.Exchange(ref accentTheme, null)?.Dispose();
        Interlocked.Exchange(ref serviceProvider, null)?.Dispose();
    }

    private class ThrowingModManager : IModManager
    {
        private readonly Exception ex;

        public ThrowingModManager(Exception ex)
        {
            this.ex = ex;
        }

        public List<ModState> FetchState() => throw ex;
        public string DisableMod(string packagePath) => throw ex;
        public string EnableMod(string packagePath) => throw ex;
        public void AddNewMod(string packagePath) => throw ex;
        public void DeleteMod(string packagePath) => throw ex;
        public void InstallEnabledMods(IEventHandler eventHandler, CancellationToken cancellationToken) => throw ex;
        public void UninstallAllMods(IEventHandler eventHandler, CancellationToken cancellationToken) => throw ex;
    }
}
