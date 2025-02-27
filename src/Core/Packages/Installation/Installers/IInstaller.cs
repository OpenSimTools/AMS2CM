using Core.Packages.Installation.Backup;
using Core.Utils;

namespace Core.Packages.Installation.Installers;

public interface IInstaller : IInstallation
{
    void Install(string dstPath, IBackupStrategy backupStrategy, ProcessingCallbacks<RootedPath> callbacks);
}
