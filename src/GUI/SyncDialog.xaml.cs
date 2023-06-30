using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AMS2CM.GUI;

public sealed partial class SyncDialog : ContentDialog
{
    private readonly CancellationTokenSource cancellationTokenSource;

    private SyncDialog(XamlRoot xamlRoot, CancellationTokenSource cancellationTokenSource)
    {
        InitializeComponent();
        XamlRoot = xamlRoot;
        this.cancellationTokenSource = cancellationTokenSource;
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        IsPrimaryButtonEnabled = false;
        Logs.Text += $"Aborting...{Environment.NewLine}";
        Progress.ShowPaused = true;
        cancellationTokenSource.Cancel();
    }

    private void SignalTermination()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (cancellationTokenSource.Token.IsCancellationRequested)
            {
                Logs.Text += $"Synchronization aborted.{Environment.NewLine}";
            }
            else
            {
                Logs.Text += $"Synchronization completed.{Environment.NewLine}";
            }
            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = true;
        });
    }

    public void SetProgress(double? progress)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Progress.IsIndeterminate = !progress.HasValue;
            Progress.Value = progress.GetValueOrDefault() * 100;
        });
    }

    public void LogMessage(string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Logs.Text += $"{message}{Environment.NewLine}";
        });
    }

    public void LogError(Exception ex)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Progress.ShowError = true;
            LogExpander.IsExpanded = true;
            Logs.Text += $"Error: {ex.Message}{Environment.NewLine}";
        });
    }

    public static async Task ShowAsync(XamlRoot xamlRoot, Action<SyncDialog, CancellationToken> action)
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        var dialog = new SyncDialog(xamlRoot, cancellationTokenSource);

        var task = Task.Run(() => {
            try
            {
                action(dialog, cancellationTokenSource.Token);
            } catch (Exception ex)
            {
                dialog.LogError(ex);
            }
            dialog.SignalTermination();
        });

        var result = dialog.ShowAsync();

        await task;
        await result;
    }
}
