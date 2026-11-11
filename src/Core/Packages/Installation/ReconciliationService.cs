using System.IO.Abstractions;
using Core.Packages.Installation.Backup;
using Core.Utils;

namespace Core.Packages.Installation;

public sealed class ReconciliationService<TEventHandler>(
    IBackupStrategyProvider<DateTimeOffset, TEventHandler> backupStrategyProvider,
    IReconciliationContext<TEventHandler>.FactoryFunc createContext)
    : IReconciliationService<TEventHandler>
    where TEventHandler : ReconciliationService.IEventHandler
{
    public void Reconcile(IReadOnlyDictionary<string, PackageInstallationState> previousState,
        IEnumerable<IPackage> packages,
        IDirectoryInfo installDir,
        Action<IReadOnlyDictionary<string, PackageInstallationState>> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var context = createContext(previousState);
        var actions = context.ReconciliationActions(packages, installDir, backupStrategyProvider, eventHandler);

        if (actions.Count == 0)
        {
            eventHandler.ReconciliationNoPackages();
            return;
        }

        try
        {
            eventHandler.ReconciliationStart();
            var progress = new PercentOfTotal(actions.Count);
            foreach (var action in actions)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                action();
                eventHandler.ProgressUpdate(progress.IncrementDone());
            }
            eventHandler.ReconciliationEnd();
        }
        finally
        {
            afterInstall(context.CurrentState);
        }
    }
}

public static class ReconciliationService
{
    public interface IEventHandler : IProgress, IReconciliationContext.IEventHandler
    {
        void ReconciliationNoPackages();
        void ReconciliationStart();
        void ReconciliationEnd();
    }

    public interface IProgress
    {
        public void ProgressUpdate(IPercent? progress);
    }
}
