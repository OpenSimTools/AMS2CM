namespace Core.Packages.Installation.Backup;

public interface IBackupStrategyProvider<in TState>
{
    IBackupStrategy BackupStrategy(TState? state);
}
