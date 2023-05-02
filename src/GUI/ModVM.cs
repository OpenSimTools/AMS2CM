using System.Runtime.CompilerServices;
using Core;
using Microsoft.UI.Composition;

namespace AMS2CM.GUI;

internal class ModVM
{
    private readonly ModState modState;
    private readonly ModManager modManager;
    private bool isEnabled;
    private string currentPackagePath;

    public ModVM(ModState modState, ModManager modManager)
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
    }

    private void DoNothing()
    {
    }
}
