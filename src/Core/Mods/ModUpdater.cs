using Core.State;

namespace Core.Mods;

public class ModUpdater : IUpdater
{
    private readonly IInstaller? installer;
    public ModInstallationState State { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="installer">Installer, or null for uninstall.</param>
    /// <param name="state">Installation state, or null if not installed.</param>
    public ModUpdater(IInstaller? installer, ModInstallationState? currentState)
    {
        this.installer = installer;
        State = currentState ?? ModInstallationState.Empty;
    }

    // Do we just need accept callaback for whitelisting, etc.?
    // Add CancellationToken to stop
    public void Update()
    {
        // Could move the driveline config in the state
        // The other config should stay outside: even if we make it configurable per mod,
        // it will have an impact on what is installed rather than what is configured!
    }
}
