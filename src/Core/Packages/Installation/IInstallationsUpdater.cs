using Core.Packages.Installation.Installers;

namespace Core.Packages.Installation;

public interface IInstallationsUpdater<in TEventHandler>
{
    void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> currentState,
        IReadOnlyCollection<IInstaller> installers,
        string installDir,
        Action<string, PackageInstallationState?> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken);
}
