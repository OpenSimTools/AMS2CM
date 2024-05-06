using System.IO.Abstractions;

namespace Core.Backup;

public class SuffixBackupStrategy : MoveFileBackupStrategy
{
    public const string BackupSuffix = ".orig";

    public SuffixBackupStrategy() :
        base(new FileSystem(), _ => $"{_}{BackupSuffix}")
    {
    }
}