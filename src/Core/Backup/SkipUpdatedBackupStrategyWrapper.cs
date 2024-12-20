using System.IO.Abstractions;

namespace Core.Backup;

/// <summary>
/// It avoids restoring backups when game files have been updated by Steam.
/// </summary>
internal class SkipUpdatedBackupStrategyWrapper : IBackupStrategy
{
    private readonly IFileSystem fs;
    private readonly IBackupStrategy inner;
    private readonly DateTime? backupTimeUtc;

    internal SkipUpdatedBackupStrategyWrapper(IBackupStrategy backupStrategy, DateTime? backupTimeUtc) :
        this(new FileSystem(), backupStrategy, backupTimeUtc)
    {
    }

    internal SkipUpdatedBackupStrategyWrapper(IFileSystem fs, IBackupStrategy backupStrategy, DateTime? backupTimeUtc)
    {
        this.fs = fs;
        inner = backupStrategy;
        this.backupTimeUtc = backupTimeUtc;
    }

    public void DeleteBackup(string fullPath) =>
        inner.DeleteBackup(fullPath);

    public void PerformBackup(string fullPath) =>
        inner.PerformBackup(fullPath);

    public bool RestoreBackup(string fullPath)
    {
        if (FileWasOverwritten(fullPath))
        {
            inner.DeleteBackup(fullPath);
            return false;
        }

        return inner.RestoreBackup(fullPath);
    }

    private bool FileWasOverwritten(string fullPath) =>
        backupTimeUtc is not null &&
        fs.File.Exists(fullPath) &&
        fs.File.GetCreationTimeUtc(fullPath) > backupTimeUtc;
}
