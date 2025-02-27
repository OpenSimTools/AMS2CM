using System.IO.Abstractions;
using Core.Utils;

namespace Core.Packages.Installation.Backup;

/// <summary>
/// It avoids restoring backups when game files have been updated by Steam.
/// </summary>
internal class SkipUpdatedBackupStrategy : IBackupStrategy
{
    internal class Provider : IBackupStrategyProvider<PackageInstallationState>
    {
        private readonly IBackupStrategy baseStrategy;

        public Provider(IBackupStrategy baseStrategy)
        {
            this.baseStrategy = baseStrategy;
        }

        public IBackupStrategy BackupStrategy(PackageInstallationState? state) =>
            new SkipUpdatedBackupStrategy(baseStrategy, state?.Time);
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
        inner.DeleteBackup(path);

    public void PerformBackup(RootedPath path) =>
        inner.PerformBackup(path);

    public bool RestoreBackup(RootedPath path)
    {
        if (FileWasOverwritten(path))
        {
            inner.DeleteBackup(path);
            return false;
        }

        return inner.RestoreBackup(path);
    }

    private bool FileWasOverwritten(RootedPath path) =>
        backupTimeUtc is not null &&
        fs.File.Exists(path.Full) &&
        fs.File.GetCreationTimeUtc(path.Full) > backupTimeUtc;

    public void AfterInstall(RootedPath path)
    {
        inner.AfterInstall(path);

        var now = DateTime.UtcNow;
        if (fs.File.Exists(path.Full) && fs.File.GetCreationTimeUtc(path.Full) > now)
        {
            fs.File.SetCreationTimeUtc(path.Full, now);
        }
    }
}
