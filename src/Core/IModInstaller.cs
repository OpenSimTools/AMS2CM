using Core.Mods;
using Core.State;

namespace Core;
public interface IModInstaller
{
    void InstallPackages(IReadOnlyCollection<ModPackage> packages, string installDir, Action<IInstallation> afterInstall, ModInstaller.IEventHandler eventHandler, CancellationToken cancellationToken);
    void UninstallPackages(InternalInstallationState currentState, string installDir, Action<IInstallation> afterUninstall, ModInstaller.IEventHandler eventHandler, CancellationToken cancellationToken);
}
