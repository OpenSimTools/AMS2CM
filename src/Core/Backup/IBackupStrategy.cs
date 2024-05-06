namespace Core.Backup;

public interface IBackupStrategy
{
    public void PerformBackup(string fullPath);
    public void RestoreBackup(string fullPath);
    public void DeleteBackup(string fullPath);
    public bool IsBackupFile(string fullPath);
}