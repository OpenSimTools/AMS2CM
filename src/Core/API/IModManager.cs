namespace Core.API;

public interface IModManager
{
    sealed List<ModState> FetchState()
    {
        throw new NotImplementedException();
    }

    string DisableMod(string packagePath);
    string EnableMod(string packagePath);
    ModState AddNewMod(string packagePath);
    void DeleteMod(string packagePath);
    void InstallEnabledMods(IEventHandler eventHandler, CancellationToken cancellationToken = default);
    void UninstallAllMods(IEventHandler eventHandler, CancellationToken cancellationToken = default);
}
