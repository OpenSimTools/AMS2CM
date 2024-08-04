namespace Core.Mods;

public interface IInstaller : IInstallation
{
    ConfigEntries Install(string dstPath, ProcessingCallbacks<RootedPath> callbacks);
}
