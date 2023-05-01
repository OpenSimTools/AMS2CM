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
        InitializeComponent();
        modManager = CreateModManager();
        SyncModListView();
    }

    private void SyncButton_Click(object sender, RoutedEventArgs e)
    {
        SyncButton.IsEnabled = false;
        modManager.InstallEnabledMods();
        SyncModListView();
        SyncButton.IsEnabled = true;
    }

    private void SyncModListView()
    {
        ModListView.ItemsSource = modManager.FetchState().Select(modState => new ModVM(modState, modManager));
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
