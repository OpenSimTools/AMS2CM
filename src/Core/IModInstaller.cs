using Core.Mods;
using Core.State;

namespace Core;
public interface IModInstaller
{
    void InstallPackages(IReadOnlyCollection<ModPackage> packages, string installDir, Action<IModInstallation> afterInstall, ModInstaller.IEventHandler eventHandler, CancellationToken cancellationToken);
    void UninstallPackages(InternalInstallationState currentState, string installDir, Action<IModInstallation> afterUninstall, ModInstaller.IEventHandler eventHandler, CancellationToken cancellationToken);
}