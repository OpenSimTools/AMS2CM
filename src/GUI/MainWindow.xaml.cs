using System.Collections.ObjectModel;
using Core.API;
using Core.SoftwareUpdates;
using Core.Utils;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinUIEx;

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
            var eventLogger = new SyncDialogEventLogger(dialog);
            modManager.InstallEnabledMods(eventLogger, cancellationToken);
            var status = cancellationToken.IsCancellationRequested ? "aborted" : "completed";
            dialog.LogMessage($"Synchronization {status}.");
        });
        SyncModListView();
    }

    private async void UninstallAllItem_Click(object sender, RoutedEventArgs e)
    {
        await SyncDialog.ShowAsync(Content.XamlRoot, (dialog, cancellationToken) =>
        {
            var eventLogger = new SyncDialogEventLogger(dialog);
            modManager.UninstallAllMods(eventLogger);
            dialog.SetProgress(1.0);
            var status = cancellationToken.IsCancellationRequested ? "aborted" : "completed";
            dialog.LogMessage($"Uninstall {status}.");
        });
        SyncModListView();
    }

    private void SyncModListView()
    {
        modList.Clear();
        foreach (var modState in modManager.FetchState().OrderBy(_ => _.PackageName))
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
        var storageItems = new List<StorageFile>();
        var filePaths = e.Items.OfType<ModVM>().SelectNotNull(_ => _.PackagePath);
        foreach (var filePath in filePaths)
        {
            if (Directory.Exists(filePath))
            {
                continue;
            }
            var si = await StorageFile.GetFileFromPathAsync(filePath);
            storageItems.Add(si);
        }

        if (!storageItems.Any())
        {
            e.Cancel = true;
            return;
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

    public async void SignalErrorAsync(Exception exception)
    {
        var dialog = new ErrorDialog(Content.XamlRoot, exception);
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
            modManager.DeleteMod(filePath);
            dialog.LogMessage(Path.GetFileName(filePath));
        });

        SyncModListView();
    }

    internal class SyncDialogEventLogger : BaseEventLogger
    {
        private readonly SyncDialog dialog;

        internal SyncDialogEventLogger(SyncDialog dialog)
        {
            this.dialog = dialog;
        }

        public override void ProgressUpdate(IPercent? value) => dialog.SetProgress(value?.Percent);
        protected override void LogMessage(string message) => dialog.LogMessage(message);
    }
}
