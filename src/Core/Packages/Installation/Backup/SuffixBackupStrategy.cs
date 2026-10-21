using System.IO.Abstractions;

namespace Core.Packages.Installation.Backup;

public class SuffixBackupStrategy : MoveFileBackupStrategy
{
    internal class Provider<TState, TEventHandler>(IFile file) : IBackupStrategyProvider<TState, TEventHandler>
        where TEventHandler : IBackupEventHandler
    {
        public IBackupStrategy BackupStrategy(TState? _, TEventHandler? eventHandler) =>
            new SuffixBackupStrategy(file, eventHandler);
    }

    private class BackupFileNaming : IBackupFileNaming
    {
        private const string BackupSuffix = ".orig";

        public string ToBackup(string fullPath) => $"{fullPath}{BackupSuffix}";
        public bool IsBackup(string fullPath) => fullPath.EndsWith(BackupSuffix);
    }

    public SuffixBackupStrategy(IFile file, IBackupEventHandler? eventHandler) :
        base(file, new BackupFileNaming(), eventHandler)
    {
    }
}
