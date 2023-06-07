namespace Core;

public interface IModManager
{
    // Temporary until proper events are provided
    public delegate void LogHandler(string logLine);

    public event LogHandler Logs;

    List<ModState> FetchState();
    string DisableMod(string packagePath);
    string EnableMod(string packagePath);
    ModState EnableNewMod(string packagePath);
    void InstallEnabledMods(CancellationToken cancellationToken = default);
}