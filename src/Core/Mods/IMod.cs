namespace Core.Mods;

public interface IMod
{
    string PackageName { get; }
    InstalledState Installed { get; }
    IReadOnlyCollection<string> InstalledFiles { get; }

    ConfigEntries Install(string dstPath, JsgmeFileInstaller.BeforeFileCallback beforeFileCallback);

    public enum InstalledState
    {
        NotInstalled = 0,
        PartiallyInstalled = 1,
        Installed = 2
    }
}
