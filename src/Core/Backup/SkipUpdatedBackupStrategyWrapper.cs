namespace Core.Backup;

internal class SkipUpdatedBackupStrategyWrapper : IBackupStrategy
{
    private readonly IBackupStrategy inner;
    private readonly DateTime backupTimeUtc;

    internal SkipUpdatedBackupStrategyWrapper(IBackupStrategy backupStrategy, DateTime backupTimeUtc)
    {
        inner = backupStrategy;
        this.backupTimeUtc = backupTimeUtc;
    }

    public void DeleteBackup(string fullPath) =>
        inner.DeleteBackup(fullPath);

    public void PerformBackup(string fullPath) =>
        inner.DeleteBackup(fullPath);

    public void RestoreBackup(string fullPath)
    {
        if (File.Exists(fullPath) && File.GetCreationTimeUtc(fullPath) > backupTimeUtc)
        {
            inner.DeleteBackup(fullPath);
        }
        else
        {
            inner.RestoreBackup(fullPath);
        }
    }
}
