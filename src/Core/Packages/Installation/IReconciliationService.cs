using System.IO.Abstractions;

namespace Core.Packages.Installation;

public interface IReconciliationService<in TEventHandler>
{
    void Reconcile(IReadOnlyDictionary<string, PackageInstallationState> previousState,
        IEnumerable<IPackage> packages,
        IDirectoryInfo installDir,
        Action<IReadOnlyDictionary<string, PackageInstallationState>> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken);
}
