namespace Core.Packages.Installation;

public interface IReconciliationService<in TEventHandler>
{
    void Reconcile(
        IReadOnlyDictionary<string, PackageInstallationState> previousState,
        IReadOnlyCollection<IPackage> packages,
        string installDir,
        Action<IReadOnlyDictionary<string, PackageInstallationState>> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken);
}
