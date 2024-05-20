using Core.Mods;

namespace Core;

public interface IInstallationFactory
{
    IInstaller GeneratedBootfilesInstaller();
    IInstaller ModInstaller(ModPackage modPackage);
}
