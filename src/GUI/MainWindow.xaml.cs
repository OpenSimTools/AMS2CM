using System.Collections.ObjectModel;
using Microsoft.VisualBasic.FileIO;
using Core;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinUIEx;
using Windows.Storage.Pickers;
using Core.Utils;
using Core.SoftwareUpdates;

namespace AMS2CM.GUI;

public sealed partial class MainWindow : WindowEx
{
    private readonly ObservableCollection<ModVM> modList;
    private readonly IModManager modManager;
    private readonly IUpdateChecker updateChecker;

    public MainWindow(IModManager modManager, IUpdateChecker updateChecker)
    {
        InitializeComponent();
        this.modManager = modManager;
        this.updateChecker = updateChecker;
        modList = new ObservableCollection<ModVM>();
        ModListView.ItemsSource = modList;
    }

    private void Root_Loaded(object sender, RoutedEventArgs e)
    {
        SyncModListView();
    }

    private async void NewVersionBlock_Loaded(object sender, RoutedEventArgs e)
    {
        if (await updateChecker.CheckUpdateAvailable())
        {
            DispatcherQueue.TryEnqueue(() => NewVersionBlock.Visibility = Visibility.Visible);
        }
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
            var status = cancellationToken.IsCancellationRequested ? "aborted" : "completed";
            dialog.LogMessage($"Synchronization {status}.");
        });
        SyncModListView();
    }

    private async void UninstallAllItem_Click(object sender, RoutedEventArgs e)
    {
        await SyncDialog.ShowAsync(Content.XamlRoot, (dialog, cancellationToken) =>
        {
            modManager.Logs += dialog.LogMessage;
            modManager.UninstallAllMods();
            dialog.SetProgress(1.0);
            modManager.Logs -= dialog.LogMessage;
            var status = cancellationToken.IsCancellationRequested ? "aborted" : "completed";
            dialog.LogMessage($"Uninstall {status}.");
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
            var storageItems = await e.DataView.GetStorageItemsAsync();
            AddNewMods(storageItems.Select(_ => _.Path));
        }
    }

    private async void ModListView_DragItemsStarting(object sender, Microsoft.UI.Xaml.Controls.DragItemsStartingEventArgs e)
    {
        var filePaths = e.Items.OfType<ModVM>().SelectNotNull(_ => _.PackagePath);
        if (!filePaths.Any())
        {
            e.Cancel = true;
            return;
        }

        var storageItems = new List<StorageFile>();
        foreach (var filePath in filePaths)
        {
            var si = await StorageFile.GetFileFromPathAsync(filePath);
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

    private void ModListMenuEnable_Click(object sender, RoutedEventArgs e)
    {
        foreach (var o in ModListView.SelectedItems)
        {
            var mvm = (ModVM)o;
            mvm.IsEnabled = true;
        }
    }

    private void ModListMenuDisable_Click(object sender, RoutedEventArgs e)
    {
        foreach (var o in ModListView.SelectedItems)
        {
            var mvm = (ModVM)o;
            mvm.IsEnabled = false;
        }
    }

    private async void ModListMenuAdd_Click(object sender, RoutedEventArgs e)
    {
        var filePicker = this.CreateOpenFilePicker();
        filePicker.ViewMode = PickerViewMode.List;
        filePicker.FileTypeFilter.Add("*");

        var storageFiles = await filePicker.PickMultipleFilesAsync();
        var filePaths = storageFiles.Select(_ => _.Path);
        AddNewMods(filePaths);
    }

    private void ModListMenuDelete_Click(object sender, RoutedEventArgs e)
    {
        var filePaths = ModListView.SelectedItems.OfType<ModVM>().SelectNotNull(_ => _.PackagePath);
        DeleteMods(filePaths);
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

    private async void AddNewMods(IEnumerable<string> filePaths)
    {
        if (!filePaths.Any())
        {
            return;
        }

        await SyncDialog.ShowAsync(Content.XamlRoot, filePaths, (dialog, filePath) =>
            {
                modManager.AddNewMod(filePath);
                dialog.LogMessage(Path.GetFileName(filePath));
            });

        SyncModListView();
    }

    private async void DeleteMods(IEnumerable<string> filePaths)
    {
        if (!filePaths.Any())
        {
            return;
        }

        await SyncDialog.ShowAsync(Content.XamlRoot, filePaths, (dialog, filePath) =>
        {
            FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            dialog.LogMessage(Path.GetFileName(filePath));
        });

        SyncModListView();
    }
}
