namespace Core;

public interface IModManager
{
    // Temporary until proper events are provided
    public delegate void LogHandler(string logLine);
    public delegate void ProgressHandler(double? progress);

    public event LogHandler? Logs;
    public event ProgressHandler? Progress;

    List<ModState> FetchState();
    string DisableMod(string packagePath);
    string EnableMod(string packagePath);
    ModState AddNewMod(string packagePath);
    void InstallEnabledMods(CancellationToken cancellationToken = default);
    void UninstallAllMods(CancellationToken cancellationToken = default);
}