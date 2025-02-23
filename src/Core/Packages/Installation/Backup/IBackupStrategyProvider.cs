namespace Core.Packages.Installation.Backup;

public interface IBackupStrategyProvider
{
    IInstallationBackupStrategy BackupStrategy(PackageInstallationState? state);
}
