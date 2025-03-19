using Core.Packages.Installation.Installers;

namespace Core.Packages.Installation;

public interface IPackagesUpdater<in TEventHandler>
{
    void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> currentState,
        IReadOnlyCollection<IInstaller> installers,
        string installDir,
        Action<string, PackageInstallationState?> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken);
}
