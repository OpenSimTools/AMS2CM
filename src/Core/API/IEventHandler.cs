using Core.Bootfiles;
using Core.Mods;

namespace Core.API;

public interface IEventHandler : BootfilesInstaller.IEventHandler, InstallationsUpdater.IEventHandler
{

}
