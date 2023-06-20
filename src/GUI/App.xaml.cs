using Core;
using Core.Games;
using Microsoft.UI.Xaml;

namespace AMS2CM.GUI;

public partial class App : Application
{
    private MainWindow? window;

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
        window.SignalErrorAsync(args.Exception.Message);
        args.Handled = true;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {

        var modManager = CreateModManager();
        window = new MainWindow(modManager);
        window.Activate();
    }

    private static IModManager CreateModManager()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            var config = Config.Load(args);
            var game = new Game(config.Game);
            var modFactory = new ModFactory(config.ModInstall, game);
            return new ModManager(game, modFactory, oldStateIsPrimary: false);
        }
        catch (Exception ex)
        {
            return new ThrowingModManager(ex);
        }
    }

    private class ThrowingModManager : IModManager
    {
        private readonly Exception ex;

        public ThrowingModManager(Exception ex)
        {
            this.ex = ex;
        }

        public event IModManager.LogHandler? Logs {
            add => throw ex;
            remove => throw ex;
        }

        public string DisableMod(string packagePath) => throw ex;
        public string EnableMod(string packagePath) => throw ex;
        public ModState EnableNewMod(string packagePath) => throw ex;
        public List<ModState> FetchState() => throw ex;
        public void InstallEnabledMods(CancellationToken cancellationToken) => throw ex;
        public void UninstallAllMods(CancellationToken cancellationToken) => throw ex;
    }
}
