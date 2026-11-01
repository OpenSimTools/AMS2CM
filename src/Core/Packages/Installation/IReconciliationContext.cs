using System.IO.Abstractions;
using Core.Packages.Installation.Backup;

namespace Core.Packages.Installation;

public interface IReconciliationContext<TEventHandler> where TEventHandler : ReconciliationService.IEventHandler
{
    public delegate IReconciliationContext<TEventHandler> FactoryFunc(
        IReadOnlyDictionary<string, PackageInstallationState> currentState);

    IReadOnlyDictionary<string, PackageInstallationState> CurrentState { get; }

    IReadOnlyCollection<IReconciliationAction> ReconciliationActions(IEnumerable<IPackage> packages,
        IDirectoryInfo installDir, IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider,
        TEventHandler eventHandler);

    void ExecuteAction(IReconciliationAction action);
}
