using System.IO.Abstractions;

namespace Core.Backup;

public class SuffixBackupStrategy : MoveFileBackupStrategy, IBackupStrategy
{
    public const string BackupSuffix = ".orig";

    public SuffixBackupStrategy() :
        base(new FileSystem(), _ => $"{_}{BackupSuffix}")
    {
    }

    public bool IsBackupFile(string fullPath) =>
        fullPath.EndsWith(BackupSuffix);
}
