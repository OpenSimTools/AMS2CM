using Core.Mods.Installation;
using Core.Packages.Installation;

namespace Core.API;

public interface IEventHandler : ReconciliationService.IEventHandler, ModReconciliationContext.IEventHandler
{
}
