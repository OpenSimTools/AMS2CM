using Core.Mods.Installation.Installers;
using Core.Packages.Installation;

namespace Core.API;

public interface IEventHandler : ReconciliationService.IEventHandler, BootfilesInstaller.IEventHandler
{
}
