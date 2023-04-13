namespace Core;

public interface IMod
{
    string PackageName { get; }
    bool Installed { get; }
    IReadOnlyCollection<string> InstalledFiles { get; }

    void Install(string dstPath);
}