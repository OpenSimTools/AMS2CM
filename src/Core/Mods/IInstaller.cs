using Core.Backup;

namespace Core.Mods;

public interface IInstaller : IInstallation
{
    ConfigEntries Install(string dstPath, IInstallationBackupStrategy backupStrategy, ProcessingCallbacks<RootedPath> callbacks);
}
