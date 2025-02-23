using Core.State;

namespace Core.Mods;

public interface IInstallationsUpdater<in TEventHandler>
{
    void Apply(
        IReadOnlyDictionary<string, ModInstallationState> currentState,
        IReadOnlyCollection<IInstaller> installers,
        string installDir,
        Action<IInstallation> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken);
}
