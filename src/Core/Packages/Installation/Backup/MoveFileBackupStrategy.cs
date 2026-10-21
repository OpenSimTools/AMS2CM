using System.IO.Abstractions;
using Core.Utils;

namespace Core.Packages.Installation.Backup;

public class MoveFileBackupStrategy : IBackupStrategy
{
    public interface IBackupFileNaming
    {
        public string ToBackup(string fullPath);
        public bool IsBackup(string fullPath);
    }

    private readonly IFile fs;
    private readonly IBackupFileNaming backupFileNaming;
    private readonly IBackupEventHandler? eventHandler;

    public MoveFileBackupStrategy(IFile fs, IBackupFileNaming backupFileNaming, IBackupEventHandler? eventHandler)
    {
        this.fs = fs;
        this.backupFileNaming = backupFileNaming;
        this.eventHandler = eventHandler;
    }

    public virtual void PerformBackup(RootedPath path)
    {
        if (backupFileNaming.IsBackup(path.Full))
        {
            throw new InvalidOperationException("Installing a backup file is forbidden");
        }

        if (!fs.Exists(path.Full))
        {
            return;
        }

        var backupFilePath = backupFileNaming.ToBackup(path.Full);

        if (fs.Exists(backupFilePath))
        {
            fs.Delete(path.Full);
            eventHandler?.BackupSkipped(path);
            return;
        }

        fs.Move(path.Full, backupFilePath);
    }

    public void RestoreBackup(RootedPath path)
    {
        if (fs.Exists(path.Full))
        {
            fs.Delete(path.Full);
        }

        var backupFilePath = backupFileNaming.ToBackup(path.Full);
        if (fs.Exists(backupFilePath))
        {
            fs.Move(backupFilePath, path.Full);
        }
    }

    public void DeleteBackup(RootedPath path)
    {
        var backupFilePath = backupFileNaming.ToBackup(path.Full);
        if (fs.Exists(backupFilePath))
        {
            fs.Delete(backupFilePath);
        }
    }

    public void AfterInstall(RootedPath path)
    {
    }
}
