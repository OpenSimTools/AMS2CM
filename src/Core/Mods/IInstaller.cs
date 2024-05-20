namespace Core.Mods;

public interface IInstaller : IInstallation, IDisposable
{
    ConfigEntries Install(string dstPath, ProcessingCallbacks<RootedPath> callbacks);
}
