namespace Core.Mods;

public record ConfigEntries(
    IReadOnlyCollection<string> CrdFileEntries,
    IReadOnlyCollection<string> TrdFileEntries,
    IReadOnlyCollection<string> DrivelineRecords
)
{
    public static readonly ConfigEntries Empty =
        new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    public bool Any() => CrdFileEntries.Any() || TrdFileEntries.Any() || DrivelineRecords.Any();

    public bool None() => !Any();
};
