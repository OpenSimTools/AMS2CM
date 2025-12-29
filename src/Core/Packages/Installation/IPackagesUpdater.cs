using Core.Packages.Repository;

namespace Core.Packages.Installation;

public interface IPackagesUpdater<in TEventHandler>
{
    void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> previousState,
        IEnumerable<Package> packages,
        string installDir,
        Action<IReadOnlyDictionary<string, PackageInstallationState>> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken);
}
