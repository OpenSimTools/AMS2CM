using System.Collections.ObjectModel;
using Core.API;
using Core.SoftwareUpdates;
using Core.Utils;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace AMS2CM.GUI;

public sealed partial class MainWindow : Window
{
    private readonly ObservableCollection<ModVm> modList = [];
    private readonly IModManager modManager;
    private readonly IUpdateChecker updateChecker;
    private TaskCompletionSource? operationCompletion;
    private bool isShowingError;

    private XamlRoot DialogRoot => Root.XamlRoot ?? throw new InvalidOperationException("The window is not loaded.");

    public MainWindow(IModManager modManager, IUpdateChecker updateChecker)
    {
        InitializeComponent();
        AppWindow.Resize(new Windows.Graphics.SizeInt32(600, 600));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "AMS2CM.ico"));
        AppWindow.Closing += (_, args) => args.Cancel = operationCompletion is not null;
        this.modManager = modManager;
        this.updateChecker = updateChecker;
        ModListView.ItemsSource = modList;
    }

    private void Root_Loaded(object sender, RoutedEventArgs e) => SyncModListView();

    private void SyncModListView()
    {
        modList.Clear();
        foreach (var modState in modManager.FetchState().OrderBy(state => state.PackageName, StringComparer.CurrentCulture))
        {
            modList.Add(new ModVm(modState, modManager));
        }
    }

    private void NewVersionBlock_Loaded(object sender, RoutedEventArgs e) => HandleUiAction(async () =>
    {
        if (await updateChecker.CheckUpdateAvailable())
        {
            NewVersionBlock.Visibility = Visibility.Visible;
        }
    });

    private void ApplyButton_Click(SplitButton sender, SplitButtonClickEventArgs args) =>
        HandleUiAction(() => RunOperationAsync(() => SyncDialog.ShowAsync(DialogRoot, (dialog, cancellationToken) =>
        {
            var eventLogger = new SyncDialogEventLogger(dialog);
            modManager.InstallEnabledMods(eventLogger, cancellationToken);
        })));

    private void UninstallAllItem_Click(object sender, RoutedEventArgs e) =>
        HandleUiAction(() => RunOperationAsync(() => SyncDialog.ShowAsync(DialogRoot, (dialog, cancellationToken) =>
        {
            var eventLogger = new SyncDialogEventLogger(dialog);
            modManager.UninstallAllMods(eventLogger, cancellationToken);
            dialog.SetProgress(1.0);
        })));

    private async void HandleUiAction(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            await SignalErrorAsync(exception);
        }
    }

    private async Task RunOperationAsync(Func<Task> action)
    {
        if (operationCompletion is not null || isShowingError)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        operationCompletion = completion;
        Root.IsEnabled = false;
        try
        {
            await action();
            SyncModListView();
        }
        finally
        {
            operationCompletion = null;
            Root.IsEnabled = !isShowingError;
            completion.SetResult();
        }
    }

    private void ModListView_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
    }

    private void ModListView_Drop(object sender, DragEventArgs e) => HandleUiAction(async () =>
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var storageItems = await e.DataView.GetStorageItemsAsync();
            await RunOperationAsync(() => AddNewModsAsync(storageItems.Select(item => item.Path)));
        }
        finally
        {
            deferral.Complete();
        }
    });

    private void ModListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        var filePaths = e.Items.OfType<ModVm>().SelectNotNull(mod => mod.PackageLocation)
            .Where(path => !Directory.Exists(path)).ToArray();
        if (filePaths.Length == 0)
        {
            e.Cancel = true;
            return;
        }

        e.Data.SetDataProvider(StandardDataFormats.StorageItems, request => ProvideStorageItems(request, filePaths));
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void ProvideStorageItems(DataProviderRequest request, IEnumerable<string> filePaths) => HandleUiAction(async () =>
    {
        var deferral = request.GetDeferral();
        try
        {
            var storageItems = new List<StorageFile>();
            foreach (var filePath in filePaths)
            {
                storageItems.Add(await StorageFile.GetFileFromPathAsync(filePath));
            }
            request.SetData(storageItems);
        }
        finally
        {
            deferral.Complete();
        }
    });

    private void ModListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) =>
        SyncModListView();

    private void ModListMenuEnable_Click(object sender, RoutedEventArgs e)
    {
        foreach (var mod in ModListView.SelectedItems.OfType<ModVm>())
        {
            mod.IsEnabled = true;
        }
    }

    private void ModListMenuDisable_Click(object sender, RoutedEventArgs e)
    {
        foreach (var mod in ModListView.SelectedItems.OfType<ModVm>())
        {
            mod.IsEnabled = false;
        }
    }

    private void ModListMenuAdd_Click(object sender, RoutedEventArgs e) =>
        HandleUiAction(() => RunOperationAsync(async () =>
        {
            var filePicker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            filePicker.FileTypeFilter.Add("*");
            var storageFiles = await filePicker.PickMultipleFilesAsync();
            await AddNewModsAsync(storageFiles.Select(file => file.Path));
        }));

    private Task AddNewModsAsync(IEnumerable<string> filePaths) =>
        SyncDialog.ShowAsync(DialogRoot, filePaths, (dialog, filePath) =>
        {
            modManager.AddNewMod(filePath);
            dialog.LogMessage(Path.GetFileName(filePath));
        });

    private void ModListMenuDelete_Click(object sender, RoutedEventArgs e) => HandleUiAction(async () =>
    {
        var filePaths = ModListView.SelectedItems.OfType<ModVm>()
            .SelectNotNull(mod => mod.PackageLocation).ToArray();
        await RunOperationAsync(() => SyncDialog.ShowAsync(DialogRoot, filePaths, (dialog, filePath) =>
        {
            modManager.DeleteMod(filePath);
            dialog.LogMessage(Path.GetFileName(filePath));
        }));
    });

    private void ModListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var mod = (e.OriginalSource as FrameworkElement)?.DataContext as ModVm;
        if (!ModListView.SelectedItems.Contains(mod))
        {
            ModListView.SelectedItem = mod;
        }
    }

    public Task SignalErrorAsync(Exception exception)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(() => ShowError(exception, completion)))
        {
            completion.SetResult();
        }
        return completion.Task;
    }

    private async void ShowError(Exception exception, TaskCompletionSource completion)
    {
        try
        {
            await ShowErrorAsync(exception);
        }
        catch (Exception dialogException)
        {
            System.Diagnostics.Trace.TraceError("Unable to handle error: {0}. Original error: {1}", dialogException, exception);
        }
        finally
        {
            completion.SetResult();
        }
    }

    private async Task ShowErrorAsync(Exception exception)
    {
        if (isShowingError)
        {
            return;
        }

        isShowingError = true;
        Root.IsEnabled = false;
        try
        {
            if (operationCompletion is not null)
            {
                await operationCompletion.Task;
            }
            var dialog = new ErrorDialog(DialogRoot, exception);
            await dialog.ShowAsync();
        }
        catch (Exception dialogException)
        {
            System.Diagnostics.Trace.TraceError("Unable to show error: {0}. Original error: {1}", dialogException, exception);
        }
        finally
        {
            Close();
        }
    }

    private sealed class SyncDialogEventLogger(SyncDialog dialog) : BaseEventLogger
    {
        public override void ProgressUpdate(IPercent? value) => dialog.SetProgress(value?.Percent);
        protected override void LogMessage(string message) => dialog.LogMessage(message);
    }
}
