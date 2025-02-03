using Core.Backup;

namespace Core.Mods;

public interface IInstaller : IInstallation
{
    void Install(string dstPath, IInstallationBackupStrategy backupStrategy, ProcessingCallbacks<RootedPath> callbacks);
}
