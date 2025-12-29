using System.IO.Abstractions;
using Core.Utils;

namespace Core.Packages.Installation.Backup;

/// <summary>
/// It avoids restoring backups when game files have been updated by Steam.
/// </summary>
internal class SkipUpdatedBackupStrategy : IBackupStrategy
{
    internal class Provider<TEventHandler> : IBackupStrategyProvider<PackageInstallationState, TEventHandler>
        where TEventHandler : IBackupEventHandler
    {
        private readonly IBackupStrategyProvider<PackageInstallationState, TEventHandler> baseProvider;

        public Provider(IBackupStrategyProvider<PackageInstallationState, TEventHandler> baseProvider)
        {
            this.baseProvider = baseProvider;
        }

        public IBackupStrategy BackupStrategy(PackageInstallationState? state, TEventHandler? eventHandler) {
            var baseStrategy = baseProvider.BackupStrategy(state, eventHandler);
            return new SkipUpdatedBackupStrategy(baseStrategy, state?.Time, eventHandler);
        }
    }

    private readonly IFileSystem fs;
    private readonly IBackupStrategy inner;
    private readonly DateTime? backupTimeUtc;
    private readonly IBackupEventHandler? eventHandler;

    private SkipUpdatedBackupStrategy(
        IBackupStrategy backupStrategy,
        DateTime? backupTimeUtc,
        IBackupEventHandler? eventHandler) :
        this(new FileSystem(), backupStrategy, backupTimeUtc, eventHandler)
    {
    }

    internal SkipUpdatedBackupStrategy(
        IFileSystem fs,
        IBackupStrategy backupStrategy,
        DateTime? backupTimeUtc,
        IBackupEventHandler? eventHandler)
    {
        this.fs = fs;
        inner = backupStrategy;
        this.backupTimeUtc = backupTimeUtc;
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
