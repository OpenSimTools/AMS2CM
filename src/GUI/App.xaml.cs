using Core;
using Core.SoftwareUpdates;
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
        var config = Config.Load(Environment.GetCommandLineArgs());
        var modManager = CreateModManager(config);
        var updateChecker = new GitHubUpdateChecker(config.Updates);
        window = new MainWindow(modManager, updateChecker);
        window.Activate();
    }

    private static IModManager CreateModManager(Config config)
    {
        try
        {
            return Init.CreateModManager(config);
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

        public event IModManager.ProgressHandler? Progress
        {
            add => throw ex;
            remove => throw ex;
        }

        public string DisableMod(string packagePath) => throw ex;
        public string EnableMod(string packagePath) => throw ex;
        public ModState AddNewMod(string packagePath) => throw ex;
        public void DeleteMod(string packagePath) => throw ex;
        public List<ModState> FetchState() => throw ex;
        public void InstallEnabledMods(CancellationToken cancellationToken) => throw ex;
        public void UninstallAllMods(CancellationToken cancellationToken) => throw ex;
    }
}
