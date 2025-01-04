using System.IO.Abstractions;

namespace Core.Backup;

public class SuffixBackupStrategy : MoveFileBackupStrategy
{
    private class BackupFileNaming : IBackupFileNaming
    {
        private const string BackupSuffix = ".orig";

        public string ToBackup(string fullPath) => $"{fullPath}{BackupSuffix}";
        public bool IsBackup(string fullPath) => fullPath.EndsWith(BackupSuffix);
    }

    public SuffixBackupStrategy() : base(new BackupFileNaming())
    {
    }
}
