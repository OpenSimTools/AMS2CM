using Windows.ApplicationModel.DataTransfer;

namespace AMS2CM.GUI;

public sealed partial class ErrorDialog : ContentDialog
{
    private string Details { get; }

    public ErrorDialog(XamlRoot xamlRoot, Exception exception)
    {
        InitializeComponent();
        XamlRoot = xamlRoot;
        Message.Text = exception.Message;
        Details = FormatDetails(exception);
    }

    private static string FormatDetails(Exception exception) =>
        $"""
        **Version**: {GitVersionInformation.InformationalVersion}
        **OS**: {Environment.OSVersion.VersionString}
        ```
        {exception}
        ```
        """;

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var dataPackage = new DataPackage();
        dataPackage.SetText(Details);
        Clipboard.SetContent(dataPackage);
        args.Cancel = true;
    }
}
