using System.IO.Abstractions;
using Core.Utils;

namespace Core.Packages.Installation.Backup;

/// <summary>
/// It avoids restoring backups when game files have been updated by Steam.
/// </summary>
internal class SkipUpdatedBackupStrategy : IBackupStrategy
{
    internal class Provider<TEventHandler> : IBackupStrategyProvider<DateTimeOffset, TEventHandler>
        where TEventHandler : IBackupEventHandler
    {
        private readonly IBackupStrategyProvider<DateTimeOffset, TEventHandler> baseProvider;

        public Provider(IBackupStrategyProvider<DateTimeOffset, TEventHandler> baseProvider)
        {
            this.baseProvider = baseProvider;
        }

        public IBackupStrategy BackupStrategy(DateTimeOffset backupTime, TEventHandler? eventHandler) {
            var baseStrategy = baseProvider.BackupStrategy(backupTime, eventHandler);
            return new SkipUpdatedBackupStrategy(baseStrategy, backupTime, eventHandler);
        }
    }

    private readonly IFileSystem fs;
    private readonly IBackupStrategy inner;
    private readonly DateTimeOffset backupTime;
    private readonly IBackupEventHandler? eventHandler;

    private SkipUpdatedBackupStrategy(
        IBackupStrategy backupStrategy,
        DateTimeOffset backupTime,
        IBackupEventHandler? eventHandler) :
        this(new FileSystem(), backupStrategy, backupTime, eventHandler)
    {
    }

    internal SkipUpdatedBackupStrategy(
        IFileSystem fs,
        IBackupStrategy backupStrategy,
        DateTimeOffset backupTime,
        IBackupEventHandler? eventHandler)
    {
        this.fs = fs;
        inner = backupStrategy;
        this.backupTime = backupTime;
        this.eventHandler = eventHandler;
    }

    public void DeleteBackup(RootedPath path) =>
        inner.DeleteBackup(path);

    public void PerformBackup(RootedPath path) =>
        inner.PerformBackup(path);

    public void RestoreBackup(RootedPath path)
    {
        if (CreationAfterBackupTime(path))
        {
            inner.DeleteBackup(path);
            eventHandler?.RestoreSkipped(path);
            return;
        }
        inner.RestoreBackup(path);
    }

    public void AfterInstall(RootedPath path)
    {
        inner.AfterInstall(path);

        if (CreationAfterBackupTime(path))
        {
            fs.File.SetCreationTimeUtc(path.Full, backupTime.UtcDateTime);
        }
    }

    private bool CreationAfterBackupTime(RootedPath path) =>
        fs.File.Exists(path.Full) &&
        fs.File.GetCreationTimeUtc(path.Full) > backupTime.UtcDateTime;
}
