using System.Collections.ObjectModel;
using Microsoft.VisualBasic.FileIO;
using Core;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinUIEx;

namespace AMS2CM.GUI;

public sealed partial class MainWindow : WindowEx
{
    private readonly ObservableCollection<ModVM> modList;
    private readonly IModManager modManager;

    public MainWindow(IModManager modManager)
    {
        InitializeComponent();
        this.modManager = modManager;
        modList = new ObservableCollection<ModVM>();
        ModListView.ItemsSource = modList;
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        SyncModListView();
    }

    private async void ApplyButton_Click(Microsoft.UI.Xaml.Controls.SplitButton sender, Microsoft.UI.Xaml.Controls.SplitButtonClickEventArgs args)
    {
        await SyncDialog.ShowAsync(Content.XamlRoot, (dialog, cancellationToken) =>
        {
            modManager.Logs += dialog.LogMessage;
            modManager.Progress += dialog.SetProgress;
            modManager.InstallEnabledMods(cancellationToken);
            modManager.Progress -= dialog.SetProgress;
            modManager.Logs -= dialog.LogMessage;
        });
        SyncModListView();
    }

    private async void UninstallAllItem_Click(object sender, RoutedEventArgs e)
    {
        await SyncDialog.ShowAsync(Content.XamlRoot, (dialog, _) =>
        {
            modManager.Logs += dialog.LogMessage;
            modManager.UninstallAllMods();
            dialog.SetProgress(1.0);
            modManager.Logs -= dialog.LogMessage;
        });
        SyncModListView();
    }

    private void SyncModListView()
    {
        modList.Clear();
        foreach (var modState in modManager.FetchState().OrderBy(_ => _.ModName).ThenBy(_ => _.PackageName))
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
                    modManager.EnableNewMod(filePath);
                }
                // Refresh list after adding mods with drag and drop
                SyncModListView();
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
        // Refresh list after removing mods with drag and drop
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
            if (mvm.PackagePath is not null)
            {
                FileSystem.DeleteFile(mvm.PackagePath, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
            }
        }
        // Refresh list after removing mods with context menu
        SyncModListView();
    }

    private void ModListView_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        // Select the mod if right click outside of selection
        var mvm = (e.OriginalSource as FrameworkElement)?.DataContext as ModVM;
        if (!ModListView.SelectedItems.Contains(mvm))
        {
            ModListView.SelectedItem = mvm;
        }
    }

    public async void SignalErrorAsync(string message)
    {
        var dialog = new ErrorDialog(Content.XamlRoot, message);
        await dialog.ShowAsync();
        Close();
    }
}
