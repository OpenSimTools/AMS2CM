using Microsoft.UI.Xaml;

namespace AMS2CM.GUI;

public sealed partial class ErrorDialog : BaseDialog
{
    public ErrorDialog(XamlRoot xamlRoot, string message) : base(xamlRoot)
    {
        InitializeComponent();
        this.Message.Text = message;
    }
}
