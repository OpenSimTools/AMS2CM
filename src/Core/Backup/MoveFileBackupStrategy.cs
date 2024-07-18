using System.IO.Abstractions;

namespace Core.Backup;

public class MoveFileBackupStrategy : IBackupStrategy
{
    public interface IBackupFileNaming
    {
        public string ToBackup(string fullPath);
        public bool IsBackup(string fullPath);
    }

    private readonly IFileSystem fs;
    private readonly IBackupFileNaming backupFileNaming;

    public MoveFileBackupStrategy(IFileSystem fs, IBackupFileNaming backupFileNaming)
    {
        this.fs = fs;
        this.backupFileNaming = backupFileNaming;
    }

    public virtual void PerformBackup(string fullPath)
    {
        if (backupFileNaming.IsBackup(fullPath))
        {
            throw new InvalidOperationException("Installing a backup file is forbidden");
        }
        if (!fs.File.Exists(fullPath))
        {
            return;
        }

        var backupFilePath = backupFileNaming.ToBackup(fullPath);
        if (fs.File.Exists(backupFilePath))
        {
            fs.File.Delete(fullPath);
        }
        else
        {
            fs.File.Move(fullPath, backupFilePath);
        }
    }

    public void RestoreBackup(string fullPath)
    {
        var backupFilePath = backupFileNaming.ToBackup(fullPath);
        if (fs.File.Exists(backupFilePath))
        {
            fs.File.Move(backupFilePath, fullPath);
        }
    }

    public void DeleteBackup(string fullPath)
    {
        var backupFilePath = backupFileNaming.ToBackup(fullPath);
        if (fs.File.Exists(backupFilePath))
        {
            fs.File.Delete(backupFilePath);
        }
    }
}
