using Core.State;

namespace Core.Mods;

public interface IInstallationsUpdater
{
    void Apply(
        IReadOnlyDictionary<string, ModInstallationState> currentState,
        IReadOnlyCollection<IInstaller> installers,
        string installDir,
        Action<IInstallation> afterInstall,
        InstallationsUpdater.IEventHandler eventHandler,
        CancellationToken cancellationToken);
}
