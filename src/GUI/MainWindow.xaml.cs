using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.VisualBasic.FileIO;
using System.Linq;
using Core;
using Core.Games;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinUIEx;

namespace AMS2CM.GUI;

public sealed partial class MainWindow : WindowEx
{
    private readonly ObservableCollection<ModVM> modList;
    private readonly ModManager modManager;

    public MainWindow()
    {
        InitializeComponent();
        modManager = CreateModManager();
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

    private async void ModListView_DragItemsStarting(object sender, Microsoft.UI.Xaml.Controls.DragItemsStartingEventArgs e)
    {
        var storageItems = new List<StorageFile>();
        foreach (var o in e.Items)
        {
            var mvm = (ModVM)o;
            var si = await StorageFile.GetFileFromPathAsync(mvm.PackagePath);
            storageItems.Add(si);
        }
        e.Data.SetStorageItems(storageItems);

        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void ModListView_DragItemsCompleted(Microsoft.UI.Xaml.Controls.ListViewBase sender, Microsoft.UI.Xaml.Controls.DragItemsCompletedEventArgs args)
    {
        SyncModListView();
    }

    private void ModListMenuToInstall_Click(object sender, RoutedEventArgs e)
    {
        foreach (var o in ModListView.SelectedItems)
        {
            var mvm = (ModVM)o;
            mvm.IsEnabled = true;
        }
    }

    private void ModListMenuDelete_Click(object sender, RoutedEventArgs e)
    {
        foreach (var o in ModListView.SelectedItems)
        {
            var mvm = (ModVM)o;
            if (mvm.IsAvailable)
            {
                FileSystem.DeleteFile(mvm.PackagePath, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
            }
        }
        SyncModListView();
    }
}
