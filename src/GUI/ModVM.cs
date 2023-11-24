using System.ComponentModel;
using Core;

namespace AMS2CM.GUI;

internal class ModVM : INotifyPropertyChanged
{
    private readonly ModState modState;
    private readonly IModManager modManager;
    private bool isEnabled;
    private readonly bool isOutOfDate;
    private string? packagePath;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ModVM(ModState modState, IModManager modManager)
    {
        this.modState = modState;
        this.modManager = modManager;
        isEnabled = modState.IsEnabled;
        packagePath = modState.PackagePath;
        isOutOfDate = modState.IsOutOfDate;
    }

    public string Name => modState.ModName;

    public string PackageName => modState.PackageName;

    public string? PackagePath => packagePath;

    public bool? IsInstalled => modState.IsInstalled;

    public bool IsOutOfDate => isOutOfDate;

    public bool IsEnabled
    {
        get => isEnabled;
        set => EnableOrDisable(value);
    }

    public bool IsAvailable
    {
        get => PackagePath is not null;
        set { }
    }

    private void EnableOrDisable(bool shouldEnable)
    {
        if (packagePath is null || shouldEnable == isEnabled)
        {
            return;
        }

        if (shouldEnable)
        {
            packagePath = modManager.EnableMod(packagePath);
        }
        else
        {
            packagePath = modManager.DisableMod(packagePath);
        }
        isEnabled = shouldEnable;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
    }
}
