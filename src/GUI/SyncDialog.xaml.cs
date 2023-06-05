using System;
using System.Threading;
using System.Threading.Tasks;
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
        IsSecondaryButtonEnabled = false;
        Logs.Text = $"Synchronization started.{Environment.NewLine}";
        this.cancellationTokenSource = cancellationTokenSource;
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        IsPrimaryButtonEnabled = false;
        Logs.Text += $"Aborting...{Environment.NewLine}";
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

    public static async Task ShowAsync(XamlRoot xamlRoot, Action<CancellationToken> action)
    {
        using var cancellationTokenSource = new CancellationTokenSource();

        var dialog = new SyncDialog(xamlRoot, cancellationTokenSource);

        var task = Task.Run(() => {
            action(cancellationTokenSource.Token);
            dialog.SignalTermination();
        });

        var result = dialog.ShowAsync();

        await task;
        await result;
    }
}
