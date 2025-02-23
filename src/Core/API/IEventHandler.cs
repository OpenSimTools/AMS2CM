using Core.Mods.Installation.Installers;
using Core.Packages.Installation;

namespace Core.API;

public interface IEventHandler : BootfilesInstaller.IEventHandler, InstallationsUpdater.IEventHandler
{

}
