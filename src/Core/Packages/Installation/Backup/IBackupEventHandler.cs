using Core.Utils;

namespace Core.Packages.Installation.Backup;

public interface IBackupEventHandler
{
    void BackupSkipped(RootedPath path);
    void RestoreSkipped(RootedPath path);
}
