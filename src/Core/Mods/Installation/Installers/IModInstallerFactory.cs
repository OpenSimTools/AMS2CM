using Core.Packages.Installation.Installers;

namespace Core.Mods.Installation.Installers;

public interface IModInstallerFactory<in TEventHandler>
{
    public IInstaller ModInstaller(IInstaller packageInstaller, IInstaller bootfilesInstaller);
    public IInstaller BootfilesInstaller(IInstaller? bootfilesPackageInstaller, TEventHandler eventHandler);
}
