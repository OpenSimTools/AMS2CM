using Core.Mods.Installation.Installers;
using Core.Packages.Installation;

namespace Core.API;

public interface IEventHandler : PackageReconciliationService.IEventHandler, BootfilesInstaller.IEventHandler
{
}
