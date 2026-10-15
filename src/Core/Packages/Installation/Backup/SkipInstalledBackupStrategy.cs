using Core.Utils;

namespace Core.Packages.Installation.Backup;

/// <summary>
/// Prevents installed package contents from being backed up as original game files.
/// </summary>
internal class SkipInstalledBackupStrategy(IBackupStrategy inner, ISet<RootedPath> installedFiles) : IBackupStrategy
{
    internal class Provider<TEventHandler>(
        IBackupStrategyProvider<DateTimeOffset, TEventHandler> baseProvider,
        IEnumerable<RootedPath> installedFiles) : IBackupStrategyProvider<DateTimeOffset, TEventHandler>
        where TEventHandler : IBackupEventHandler
    {
        // All actions in a reconciliation must observe the same evolving installation state.
        private readonly HashSet<RootedPath> currentFiles = [.. installedFiles];

        public IBackupStrategy BackupStrategy(DateTimeOffset backupTime, TEventHandler? eventHandler) =>
            new SkipInstalledBackupStrategy(baseProvider.BackupStrategy(backupTime, eventHandler), currentFiles);
    }

    public void PerformBackup(RootedPath path)
    {
        if (!installedFiles.Contains(path))
        {
            inner.PerformBackup(path);
        }
    }

    public void AfterInstall(RootedPath path)
    {
        inner.AfterInstall(path);
        installedFiles.Add(path);
    }

    public void RestoreBackup(RootedPath path)
    {
        inner.RestoreBackup(path);
        installedFiles.Remove(path);
    }

    public void DeleteBackup(RootedPath path) => inner.DeleteBackup(path);
}
