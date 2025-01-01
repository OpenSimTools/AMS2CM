using Core.State;

namespace Core.Backup;

public interface IBackupStrategyProvider
{
    IInstallationBackupStrategy BackupStrategy(DateTime? backupTimeUtc);
}
