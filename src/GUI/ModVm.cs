using System.ComponentModel;
using Core.API;

namespace AMS2CM.GUI;

internal class ModVm : INotifyPropertyChanged
{
    private readonly ModState modState;
    private readonly IModManager modManager;
    private bool isEnabled;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ModVm(ModState modState, IModManager modManager)
    {
        this.modState = modState;
        this.modManager = modManager;
        isEnabled = modState.IsEnabled;
        PackageLocation = modState.PackageLocation;
    }

    public string DisplayName =>
        Path.EndsInDirectorySeparator(modState.PackageName)
            ? Path.TrimEndingDirectorySeparator(modState.PackageName)
            : Path.GetFileNameWithoutExtension(modState.PackageName);

    public string PackageName => modState.PackageName;
    public string? PackageLocation { get; private set; }
    public bool? IsInstalled => modState.IsInstalled;
    public bool IsOutOfDate => modState.IsOutOfDate;

    public bool IsEnabled
    {
        get => isEnabled;
        set => EnableOrDisable(value);
    }

    public bool IsAvailable => PackageLocation is not null;

    private void EnableOrDisable(bool shouldEnable)
    {
        if (PackageLocation is null || shouldEnable == isEnabled)
        {
            return;
        }

        PackageLocation = shouldEnable
            ? modManager.EnableMod(PackageLocation)
            : modManager.DisableMod(PackageLocation);
        isEnabled = shouldEnable;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
    }
}
