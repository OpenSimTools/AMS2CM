using Core.Packages.Installation.Installers;

namespace Core.Mods.Installation.Installers;

public interface IModInstallerFactory<in TEventHandler>
{
    public IPackageInstaller ModInstaller(IPackageInstaller packageInstaller, IPackageInstaller bootfilesInstaller);
    public IPackageInstaller BootfilesInstaller(IPackageInstaller? bootfilesPackageInstaller, TEventHandler eventHandler);
}
