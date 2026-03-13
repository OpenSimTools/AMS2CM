using System.Collections.Immutable;

namespace Core.Mods.Installation.Installers;

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

    public ConfigEntries Combine(ConfigEntries other) =>
        Combine(this, other);

    public static ConfigEntries Combine(ConfigEntries first, ConfigEntries second) =>
        new (
            first.CrdFileEntries.Concat(second.CrdFileEntries).ToImmutableList(),
            first.TrdFileEntries.Concat(second.TrdFileEntries).ToImmutableList(),
            first.DrivelineRecords.Concat(second.DrivelineRecords).ToImmutableList()
        );

};
