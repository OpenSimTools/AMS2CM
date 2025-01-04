using Core.State;

namespace Core.Backup;

public interface IModBackupStrategyProvider
{
    IInstallationBackupStrategy BackupStrategy(ModInstallationState? state);
}
