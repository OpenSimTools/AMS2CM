namespace Core.Packages.Installation.Backup;

public interface IBackupStrategy
{
    public void PerformBackup(string fullPath);
    public bool RestoreBackup(string fullPath);
    public void DeleteBackup(string fullPath);
}
