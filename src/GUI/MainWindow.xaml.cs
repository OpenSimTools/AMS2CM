using System;
using System.Collections.ObjectModel;
using System.Linq;
using Core;
using Core.Games;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinUIEx;

namespace AMS2CM.GUI;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<ModVM> modList;
    private readonly ModManager modManager;

    public MainWindow()
    {
        InitializeComponent();
        modManager = CreateModManager();
        this.SetWindowSize(600,600);
        modList = new ObservableCollection<ModVM>();
        ModListView.ItemsSource = modList;
        SyncModListView();
    }

    private static ModManager CreateModManager()
    {
        var args = Environment.GetCommandLineArgs();
        var config = Config.Load(args);
        var game = new Game(config.Game);
        var modFactory = new ModFactory(config.ModInstall, game);
        return new ModManager(game, modFactory);
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
        modList.Clear();
        foreach (var modState in modManager.FetchState())
        {
            modList.Add(new ModVM(modState, modManager));
        }
    }

    private void ModListView_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private async void ModListView_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            if (items.Count > 0)
            {
                foreach (var storageFile in items.OfType<StorageFile>())
                {
                    var filePath = storageFile.Path;
                    var modState = modManager.EnableNewMod(filePath);
                    modList.Add(new ModVM(modState, modManager));
                }
            }
        }
    }
}
