using System.IO.Abstractions;
using Core.Mods;

namespace Core.Backup;

/// <summary>
/// It avoids restoring backups when game files have been updated by Steam.
/// </summary>
internal class SkipUpdatedBackupStrategy : IInstallationBackupStrategy
{
    internal class Provider : IBackupStrategyProvider
    {
        private readonly IBackupStrategy defaultStrategy;

        public Provider(IBackupStrategy defaultStrategy)
        {
            this.defaultStrategy = defaultStrategy;
        }

        public IInstallationBackupStrategy BackupStrategy(DateTime? backupTimeUtc) =>
            new SkipUpdatedBackupStrategy(defaultStrategy, backupTimeUtc);
    }

    private readonly IFileSystem fs;
    private readonly IBackupStrategy inner;
    private readonly DateTime? backupTimeUtc;

    private SkipUpdatedBackupStrategy(
        IBackupStrategy backupStrategy,
        DateTime? backupTimeUtc) :
        this(new FileSystem(), backupStrategy, backupTimeUtc)
    {
    }

    internal SkipUpdatedBackupStrategy(
        IFileSystem fs,
        IBackupStrategy backupStrategy,
        DateTime? backupTimeUtc)
    {
        this.fs = fs;
        inner = backupStrategy;
        this.backupTimeUtc = backupTimeUtc;
    }

    public void DeleteBackup(RootedPath path) =>
        inner.DeleteBackup(path.Full);

    public void PerformBackup(RootedPath path) =>
        inner.PerformBackup(path.Full);

    public bool RestoreBackup(RootedPath path)
    {
        if (FileWasOverwritten(path))
        {
            inner.DeleteBackup(path.Full);
            return false;
        }

        return inner.RestoreBackup(path.Full);
    }

    private bool FileWasOverwritten(RootedPath path) =>
        backupTimeUtc is not null &&
        fs.File.Exists(path.Full) &&
        fs.File.GetCreationTimeUtc(path.Full) > backupTimeUtc;

    public void AfterInstall(RootedPath path)
    {
        var now = DateTime.UtcNow;
        if (fs.File.Exists(path.Full) && fs.File.GetCreationTimeUtc(path.Full) > now)
        {
            fs.File.SetCreationTimeUtc(path.Full, now);
        }
    }
}
