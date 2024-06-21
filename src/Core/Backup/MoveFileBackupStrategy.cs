using System.IO.Abstractions;

namespace Core.Backup;

public class MoveFileBackupStrategy
{

    private readonly IFileSystem fs;
    private readonly Func<string, string> generateBackupFilePath;

    public MoveFileBackupStrategy(IFileSystem fs, Func<string, string> generateBackupFilePath)
    {
        this.fs = fs;
        this.generateBackupFilePath = generateBackupFilePath;
    }

    public void PerformBackup(string fullPath)
    {
        if (!fs.File.Exists(fullPath))
        {
            return;
        }

        var backupFilePath = generateBackupFilePath(fullPath);
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
        var backupFilePath = generateBackupFilePath(fullPath);
        if (fs.File.Exists(backupFilePath))
        {
            fs.File.Move(backupFilePath, fullPath);
        }
    }

    public void DeleteBackup(string fullPath)
    {
        var backupFilePath = generateBackupFilePath(fullPath);
        if (fs.File.Exists(backupFilePath))
        {
            fs.File.Delete(backupFilePath);
        }
    }
}
