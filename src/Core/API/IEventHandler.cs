using Core.Mods.Installation.Installers;
using Core.Packages.Installation;

namespace Core.API;

public interface IEventHandler : PackagesUpdater.IEventHandler, BootfilesInstaller.IEventHandler
{
}
