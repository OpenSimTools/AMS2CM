using Core.Packages.Installation.Installers;
using Core.Packages.Repository;

namespace Core.Packages.Installation;

public class FileSystemInstallerFactory : IInstallerFactory
{
    public IInstaller PackageInstaller(Package package) =>
        Directory.Exists(package.FullPath)
            ? new DirectoryInstaller(package.Name, package.FsHash, package.FullPath)
            : new ArchiveInstaller(package.Name, package.FsHash, package.FullPath);
}
