using Core.Games;
using Core;
using System;
using Microsoft.UI.Xaml;

namespace AMS2CM.GUI;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // TODO Display error if it fails!
        var modManager = CreateModManager();
        window = new MainWindow(modManager);
        window.Activate();
    }

    private static ModManager CreateModManager()
    {
        var args = Environment.GetCommandLineArgs();
        var config = Config.Load(args);
        var game = new Game(config.Game);
        var modFactory = new ModFactory(config.ModInstall, game);
        return new ModManager(game, modFactory);
    }

    private Window window;
}
