using Core.Backup;

namespace Core.Mods;

public interface IInstaller : IInstallation
{
    ConfigEntries Install(string dstPath, IBackupStrategy backupStrategy, ProcessingCallbacks<RootedPath> callbacks);
}
