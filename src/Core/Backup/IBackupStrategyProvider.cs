using Core.State;

namespace Core.Backup;

public interface IBackupStrategyProvider
{
    IBackupStrategy BackupStrategy(DateTime? installationTime);
}
