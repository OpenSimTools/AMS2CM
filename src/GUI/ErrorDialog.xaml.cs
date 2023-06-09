using Microsoft.UI.Xaml.Controls;

namespace AMS2CM.GUI;

public sealed partial class ErrorDialog : ContentDialog
{
    public ErrorDialog(Microsoft.UI.Xaml.XamlRoot xamlRoot, string message)
    {
        InitializeComponent();
        XamlRoot = xamlRoot;
        Message.Text = message;
    }
}
