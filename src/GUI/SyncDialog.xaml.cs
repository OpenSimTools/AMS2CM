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

    /// <summary>
    /// Show dialog and never close it automatically
    /// </summary>
    /// <param name="xamlRoot"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static async Task ShowAsync(XamlRoot xamlRoot, Action<SyncDialog, CancellationToken> action) =>
        await ShowAsync(xamlRoot, (sd, ct) =>
        {
            action(sd, ct);
            return false;
        });

    /// <summary>
    /// Show dialog and close it automatically if function returns true
    /// </summary>
    /// <param name="xamlRoot"></param>
    /// <param name="func"></param>
    /// <returns></returns>
    public static async Task ShowAsync(XamlRoot xamlRoot, Func<SyncDialog, CancellationToken, bool> func)
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        var dialog = new SyncDialog(xamlRoot, cancellationTokenSource);

        var shouldCloseDialog = Task<bool>.Factory.StartNew(() =>
        {
            var closeDialog = false;
            try
            {
                closeDialog = func(dialog, cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                dialog.LogError(ex);
            }
            dialog.SignalTermination();
            return closeDialog;
        });

        var result = dialog.ShowAsync();

        if (await shouldCloseDialog)
        {
            result.Cancel();
        }
        else
        {
            await result;
        }
    }
}
