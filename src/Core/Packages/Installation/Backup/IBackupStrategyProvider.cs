namespace Core.Packages.Installation.Backup;

public interface IBackupStrategyProvider<in TState, in TEventHandler>
{
    IBackupStrategy BackupStrategy(TState? state, TEventHandler? eventHandler);
}
