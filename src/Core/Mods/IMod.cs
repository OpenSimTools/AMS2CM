namespace Core.Mods;

public interface IMod : IModInstallation
{
    ConfigEntries Install(string dstPath, ProcessingCallbacks<GamePath> callbacks);
}
