using System.IO.Abstractions;
using Core.Packages.Installation.Backup;

namespace Core.Packages.Installation;

public interface IReconciliationContext<TEventHandler> where TEventHandler : IReconciliationContext.IEventHandler
{
    public delegate IReconciliationContext<TEventHandler> FactoryFunc(
        IReadOnlyDictionary<string, PackageInstallationState> currentState);

    IReadOnlyDictionary<string, PackageInstallationState> CurrentState { get; }

    IReadOnlyCollection<Action> ReconciliationActions(IEnumerable<IPackage> packages,
        IDirectoryInfo installDir, IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider,
        TEventHandler eventHandler);
}

public interface IReconciliationContext
{
    public interface IEventHandler : IBackupEventHandler
    {
        void InstallingPackage(string packageName);
        void SkippingPackage(string packageName);
        void UninstallingPackage(string packageName);
    }
}
