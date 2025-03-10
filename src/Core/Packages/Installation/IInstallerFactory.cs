using Core.Packages.Installation.Installers;
using Core.Packages.Repository;

namespace Core.Packages.Installation;

public interface IInstallerFactory
{
    IInstaller PackageInstaller(Package package);
}
