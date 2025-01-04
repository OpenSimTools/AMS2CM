using Core.Mods;

namespace Core.Backup;

public interface IInstallationBackupStrategy
{
    public void PerformBackup(RootedPath path);
    public bool RestoreBackup(RootedPath path);
    public void DeleteBackup(RootedPath path);
    public void AfterInstall(RootedPath path);
}
