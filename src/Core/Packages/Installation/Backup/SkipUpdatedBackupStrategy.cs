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
        private readonly IFileSystem fs;
        private readonly IFileUpdateTracker updateTracker;
        private readonly IBackupStrategyProvider<DateTimeOffset, TEventHandler> baseProvider;

        public Provider(IFileSystem fs, IFileUpdateTracker updateTracker,
            IBackupStrategyProvider<DateTimeOffset, TEventHandler> baseProvider)
        {
            this.fs = fs;
            this.updateTracker = updateTracker;
            this.baseProvider = baseProvider;
        }

        public IBackupStrategy BackupStrategy(DateTimeOffset backupTime, TEventHandler? eventHandler)
        {
            var baseStrategy = baseProvider.BackupStrategy(backupTime, eventHandler);
            return new SkipUpdatedBackupStrategy(fs, updateTracker, baseStrategy, backupTime, eventHandler);
        }
    }

    private readonly IFileSystem fs;
    private readonly IFileUpdateTracker updateTracker;
    private readonly IBackupStrategy inner;
    private readonly DateTimeOffset backupTime;
    private readonly IBackupEventHandler? eventHandler;

    internal SkipUpdatedBackupStrategy(IFileSystem fs,
        IFileUpdateTracker updateTracker,
        IBackupStrategy backupStrategy,
        DateTimeOffset backupTime,
        IBackupEventHandler? eventHandler)
    {
        this.fs = fs;
        this.updateTracker = updateTracker;
        inner = backupStrategy;
        this.backupTime = backupTime;
        this.eventHandler = eventHandler;
    }

    public void PerformBackup(RootedPath path) =>
        inner.PerformBackup(path);

    public void RestoreBackup(RootedPath path)
    {
        if (updateTracker.HasBeenUpdated(fs.FileInfo.New(path.Full), backupTime))
        {
            inner.DeleteBackup(path);
            eventHandler?.RestoreSkipped(path);
            return;
        }

        inner.RestoreBackup(path);
    }

    public void DeleteBackup(RootedPath path) =>
        inner.DeleteBackup(path);

    public void AfterInstall(RootedPath path)
    {
        inner.AfterInstall(path);

        updateTracker.TrackUpdates(fs.FileInfo.New(path.Full), backupTime);
    }
}
