using System;
using System.Linq;
using Core;
using Core.Games;
using Microsoft.UI.Xaml;

namespace AMS2CM.GUI;

public sealed partial class MainWindow : Window
{
    private readonly ModManager modManager;

    public MainWindow()
    {
        this.InitializeComponent();
        modManager = CreateModManager();
        ModListView.ItemsSource = modManager.FetchState().Select(modState => new ModVM {
            PackageName = modState.PackageName,
            PackagePath = modState.PackagePath,
            IsInstalled = modState.IsInstalled,
            IsEnabled = modState.IsEnabled ?? false,
            IsInstallable = modState.IsEnabled is not null,
        });
    }

    private static ModManager CreateModManager()
    {
        var args = Environment.GetCommandLineArgs();
        var config = Config.Load(args);
        var game = new Game(config.Game);
        var modFactory = new ModFactory(config.ModInstall, game);
        return new ModManager(game, modFactory);
    }
}
