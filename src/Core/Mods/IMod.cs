namespace Core.Mods;

public interface IMod
{
    string PackageName { get; }
    InstalledState Installed { get; }
    IReadOnlyCollection<string> InstalledFiles { get; }
    ConfigEntries Config { get; }

    void Install(string dstPath);

    public record ConfigEntries(
        IReadOnlyCollection<string> CrdFileEntries,
        IReadOnlyCollection<string> TrdFileEntries,
        IReadOnlyCollection<string> DrivelineRecords
    )
    {
        public bool NotEmpty() => CrdFileEntries.Any() || TrdFileEntries.Any() || DrivelineRecords.Any();
    };

    public enum InstalledState
    {
        NotInstalled = 0,
        PartiallyInstalled = 1,
        Installed = 2
    }
}
