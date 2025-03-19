using Core.Utils;

namespace Core.Packages.Installation.Backup;

public interface IBackupStrategy
{
    public void PerformBackup(RootedPath path);
    public bool RestoreBackup(RootedPath path);
    public void DeleteBackup(RootedPath path);
    public void AfterInstall(RootedPath path);
}
