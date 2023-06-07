using System.ComponentModel;
using Core;

namespace AMS2CM.GUI;

internal class ModVM : INotifyPropertyChanged
{
    private readonly ModState modState;
    private readonly IModManager modManager;
    private bool isEnabled;
    private string currentPackagePath;

    public event PropertyChangedEventHandler PropertyChanged;

    public ModVM(ModState modState, IModManager modManager)
    {
        this.modState = modState;
        this.modManager = modManager;
        isEnabled = modState.IsEnabled ?? false;
        currentPackagePath = modState.PackagePath;
    }

    public string PackageName => modState.PackageName;

    public string PackagePath => currentPackagePath;

    public bool IsInstalled => modState.IsInstalled;

    public bool IsEnabled
    {
        get => isEnabled;
        set => EnableOrDisable(value);
    }

    public bool IsAvailable
    {
        get => modState.IsEnabled is not null;
        set => DoNothing();
    }

    private void EnableOrDisable(bool shouldEnable)
    {
        if (!IsAvailable || shouldEnable == isEnabled)
            return;

        if (shouldEnable)
        {
            currentPackagePath = modManager.EnableMod(currentPackagePath);
        }
        else
        {
            currentPackagePath = modManager.DisableMod(currentPackagePath);
        }
        isEnabled = shouldEnable;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
    }

    private void DoNothing()
    {
    }
}
