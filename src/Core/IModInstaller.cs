using Core.Mods;
using Core.State;

namespace Core;

public interface IModInstaller
{
    void Apply(IReadOnlyDictionary<string, InternalModInstallationState> currentState, IReadOnlyCollection<ModPackage> packages, string installDir, Action<IInstallation> afterInstall, ModInstaller.IEventHandler eventHandler, CancellationToken cancellationToken);
}
