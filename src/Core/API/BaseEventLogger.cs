using Core.Utils;

namespace Core.API;

/// <summary>
/// This class is here because of the CLI. Move it into the GUI once the CLI
/// can be decommissioned or into a shared module that handles localisation.
/// </summary>
public abstract class BaseEventLogger : IEventHandler
{
    public abstract void ProgressUpdate(IPercent? progress);
    protected abstract void LogMessage(string message);

    public void InstallNoPackages() =>
        LogMessage($"No mod archives to install");
    public void InstallStart() =>
        LogMessage("Installing mods:");
    public void InstallCurrent(string packageName) =>
        LogMessage($"- {packageName}");
    public void InstallEnd()
    {
    }

    public void PostProcessingNotRequired() =>
        LogMessage("Post-processing not required");
    public void PostProcessingStart() =>
        LogMessage("Post-processing:");
    public void ExtractingBootfiles(string? packageName) =>
        LogMessage($"Extracting bootfiles from {packageName ?? "game"}");
    public void PostProcessingVehicles() =>
        LogMessage("- Appending crd file entries");
    public void PostProcessingTracks() =>
        LogMessage("- Appending trd file entries");
    public void PostProcessingDrivelines() =>
        LogMessage("- Appending driveline records");
    public void PostProcessingEnd()
    {
    }

    public void UninstallNoPackages() =>
        LogMessage("No previously installed mods found. Skipping uninstall phase.");
    public void UninstallStart() =>
        LogMessage($"Uninstalling mods:");
    public void UninstallCurrent(string packageName) =>
        LogMessage($"- {packageName}");
    public void UninstallEnd()
    {
    }

    public void BackupSkipped(RootedPath path) =>
        LogMessage($"  Skipping backup of {path.Full}");
    public void RestoreSkipped(RootedPath path) =>
        LogMessage($"  Skipping restore of {path.Full}");
}
