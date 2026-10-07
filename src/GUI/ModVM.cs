using System.ComponentModel;
using Core;
using Core.API;

namespace AMS2CM.GUI;

internal class ModVM : INotifyPropertyChanged
{
    private readonly ModState modState;
    private readonly IModManager modManager;
    private bool isEnabled;
    private readonly bool isOutOfDate;
    private string? packageLocation;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ModVM(ModState modState, IModManager modManager)
    {
        this.modState = modState;
        this.modManager = modManager;
        isEnabled = modState.IsEnabled;
        packageLocation = modState.PackageLocation;
        isOutOfDate = modState.IsOutOfDate;
    }

    public string DisplayName =>
        Path.EndsInDirectorySeparator(modState.PackageName)
        ? Path.TrimEndingDirectorySeparator(modState.PackageName)
        : Path.GetFileNameWithoutExtension(modState.PackageName);

    public string PackageName => modState.PackageName;

    public string? PackageLocation => packageLocation;

    public bool? IsInstalled => modState.IsInstalled;

    public bool IsOutOfDate => isOutOfDate;

    public bool IsEnabled
    {
        get => isEnabled;
        set => EnableOrDisable(value);
    }

    public bool IsAvailable
    {
        get => PackageLocation is not null;
        set { }
    }

    private void EnableOrDisable(bool shouldEnable)
    {
        if (packageLocation is null || shouldEnable == isEnabled)
        {
            return;
        }

        if (shouldEnable)
        {
            packageLocation = modManager.EnableMod(packageLocation);
        }
        else
        {
            packageLocation = modManager.DisableMod(packageLocation);
        }
        isEnabled = shouldEnable;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
    }
}
