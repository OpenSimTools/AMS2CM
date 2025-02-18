using Core.State;

namespace Core.Mods;

public interface IModPackagesUpdater
{
    void Apply(
        IReadOnlyDictionary<string, ModInstallationState> currentState,
        IEnumerable<ModPackage> packages,
        string installDir,
        Action<IInstallation> afterInstall,
        InstallationsUpdater.IEventHandler eventHandler,
        CancellationToken cancellationToken);
}
