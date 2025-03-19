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

    private readonly IFileSystem fs;
    private readonly IBackupFileNaming backupFileNaming;

    public MoveFileBackupStrategy(IBackupFileNaming backupFileNaming) :
        this(new FileSystem(), backupFileNaming)
    {
    }

    public MoveFileBackupStrategy(IFileSystem fs, IBackupFileNaming backupFileNaming)
    {
        this.fs = fs;
        this.backupFileNaming = backupFileNaming;
    }

    public virtual void PerformBackup(RootedPath path)
    {
        if (backupFileNaming.IsBackup(path.Full))
        {
            throw new InvalidOperationException("Installing a backup file is forbidden");
        }
        if (!fs.File.Exists(path.Full))
        {
            return;
        }

        var backupFilePath = backupFileNaming.ToBackup(path.Full);
        if (fs.File.Exists(backupFilePath))
        {
            fs.File.Delete(path.Full);
        }
        else
        {
            fs.File.Move(path.Full, backupFilePath);
        }
    }

    public bool RestoreBackup(RootedPath path)
    {
        if (fs.File.Exists(path.Full))
        {
            fs.File.Delete(path.Full);
        }
        var backupFilePath = backupFileNaming.ToBackup(path.Full);
        if (fs.File.Exists(backupFilePath))
        {
            fs.File.Move(backupFilePath, path.Full);
        }

        return true;
    }

    public void DeleteBackup(RootedPath path)
    {
        var backupFilePath = backupFileNaming.ToBackup(path.Full);
        if (fs.File.Exists(backupFilePath))
        {
            fs.File.Delete(backupFilePath);
        }
    }

    public void AfterInstall(RootedPath path)
    {
    }
}
