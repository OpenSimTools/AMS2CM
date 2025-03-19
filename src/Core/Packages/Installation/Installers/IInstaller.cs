using Core.Packages.Installation.Backup;
using Core.Utils;

namespace Core.Packages.Installation.Installers;

public interface IInstaller : IInstallation
{
    delegate RootedPath Destination(string packagePath);

    void Install(Destination destination, IBackupStrategy backupStrategy, ProcessingCallbacks<RootedPath> callbacks);

    /// <summary>
    /// Directories, relative to the source root.
    /// </summary>
    IEnumerable<string> RelativeDirectoryPaths { get; }
}
