using System.IO.Abstractions;
using Core.Utils;

namespace Core.Packages.Installation.Backup;

/// <summary>
/// It avoids restoring backups when game files have been updated by Steam.
/// </summary>
internal class SkipUpdatedBackupStrategy : IBackupStrategy
{
    internal class Provider<TEventHandler> : IBackupStrategyProvider<DateTime, TEventHandler>
        where TEventHandler : IBackupEventHandler
    {
        private readonly IBackupStrategyProvider<DateTime, TEventHandler> baseProvider;

        public Provider(IBackupStrategyProvider<DateTime, TEventHandler> baseProvider)
        {
            this.baseProvider = baseProvider;
        }

        public IBackupStrategy BackupStrategy(DateTime backupTime, TEventHandler? eventHandler) {
            var baseStrategy = baseProvider.BackupStrategy(backupTime, eventHandler);
            return new SkipUpdatedBackupStrategy(baseStrategy, backupTime, eventHandler);
        }
    }

    private readonly IFileSystem fs;
    private readonly IBackupStrategy inner;
    private readonly DateTime backupTime;
    private readonly IBackupEventHandler? eventHandler;

    private SkipUpdatedBackupStrategy(
        IBackupStrategy backupStrategy,
        DateTime backupTime,
        IBackupEventHandler? eventHandler) :
        this(new FileSystem(), backupStrategy, backupTime, eventHandler)
    {
    }

    internal SkipUpdatedBackupStrategy(
        IFileSystem fs,
        IBackupStrategy backupStrategy,
        DateTime backupTime,
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
        if (FileWasOverwritten(path))
        {
            inner.DeleteBackup(path);
            eventHandler?.RestoreSkipped(path);
            return;
        }
        inner.RestoreBackup(path);
    }

    private bool FileWasOverwritten(RootedPath path) =>
        fs.File.Exists(path.Full) &&
        fs.File.GetCreationTimeUtc(path.Full) > backupTime;

    public void AfterInstall(RootedPath path)
    {
        inner.AfterInstall(path);

        if (fs.File.Exists(path.Full) && fs.File.GetCreationTimeUtc(path.Full) > backupTime)
        {
            fs.File.SetCreationTimeUtc(path.Full, backupTime);
        }
    }
}
