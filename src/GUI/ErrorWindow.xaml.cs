using System;
using WinUIEx;

namespace AMS2CM.GUI;

public sealed partial class ErrorWindow : WindowEx
{
    private readonly string message;

    public ErrorWindow(string message)
    {
        InitializeComponent();
        this.message = message;
    }

    private async void Grid_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var dialog = new ErrorDialog(Content.XamlRoot, message);
        await dialog.ShowAsync();
        Close();
    }
}
