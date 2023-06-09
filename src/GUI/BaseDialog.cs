using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AMS2CM.GUI;

public class BaseDialog : ContentDialog
{
    public BaseDialog(XamlRoot xamlRoot)
    {
        XamlRoot = xamlRoot;
    }
}
