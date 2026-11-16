using Core.Utils;

namespace AMS2CM.GUI;

public sealed partial class SyncDialog : ContentDialog
{
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly TaskCompletionSource opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool hasTerminated;

    private SyncDialog(XamlRoot xamlRoot, CancellationTokenSource cancellationTokenSource)
    {
        InitializeComponent();
        XamlRoot = xamlRoot;
        this.cancellationTokenSource = cancellationTokenSource;
        Opened += (_, _) => opened.TrySetResult();
        Closing += (_, args) => args.Cancel = !hasTerminated;
    }

    public static async Task ShowAsync<T>(XamlRoot xamlRoot, IEnumerable<T> enumerable, Action<SyncDialog, T> action)
    {
        var items = enumerable.ToList();
        if (items.Count == 0)
        {
            return;
        }

        await ShowAsync(xamlRoot, (dialog, token) =>
        {
            var progress = new PercentOfTotal(items.Count);
            foreach (var item in items)
            {
                token.ThrowIfCancellationRequested();
                action(dialog, item);
                dialog.SetProgress(progress.IncrementDone().Percent);
            }
            return true;
        });
    }

    public static Task ShowAsync(XamlRoot xamlRoot, Action<SyncDialog, CancellationToken> action) =>
        ShowAsync(xamlRoot, (dialog, token) =>
        {
            action(dialog, token);
            return false;
        });

    private static async Task ShowAsync(XamlRoot xamlRoot, Func<SyncDialog, CancellationToken, bool> action)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var dialog = new SyncDialog(xamlRoot, cancellationTokenSource);
        var showTask = dialog.ShowAsync().AsTask(CancellationToken.None);
        if (!await WaitForOpenAsync(dialog.opened.Task, showTask))
        {
            return;
        }
        var shouldClose = false;
        try
        {
            shouldClose = await Task.Run(() => action(dialog, cancellationTokenSource.Token), cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            dialog.LogMessage("Aborted.");
        }
        catch (Exception exception)
        {
            dialog.LogError(exception);
        }

        dialog.hasTerminated = true;
        dialog.IsPrimaryButtonEnabled = false;
        dialog.IsSecondaryButtonEnabled = true;
        if (shouldClose && !cancellationTokenSource.IsCancellationRequested)
        {
            dialog.Hide();
        }
        await showTask;
    }

    internal static async Task<bool> WaitForOpenAsync(Task openedTask, Task showTask)
    {
        var firstCompleted = await Task.WhenAny(openedTask, showTask);
        if (firstCompleted == openedTask)
        {
            return true;
        }
        await showTask;
        return false;
    }

    private void LogError(Exception exception)
    {
        Progress.ShowError = true;
        LogExpander.IsExpanded = true;
        Logs.Text += $"Error: {exception.Message}{Environment.NewLine}";
    }

    public void LogMessage(string message) =>
        DispatcherQueue.TryEnqueue(() => Logs.Text += $"{message}{Environment.NewLine}");

    public void SetProgress(double? progress) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            Progress.IsIndeterminate = !progress.HasValue;
            Progress.Value = progress.GetValueOrDefault() * 100;
        });

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        IsPrimaryButtonEnabled = false;
        Logs.Text += $"Aborting...{Environment.NewLine}";
        Progress.ShowPaused = true;
        cancellationTokenSource.Cancel();
    }
}
