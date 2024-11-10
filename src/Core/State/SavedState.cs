using System.Collections.Immutable;

namespace Core.State;

public record SavedState(
        InstallationState Install
    )
{
    public static SavedState Empty() => new(
        Install: InstallationState.Empty()
    );
};

public record InstallationState(
    // TODO: needed for backward compatibility
    DateTime? Time,
    IReadOnlyDictionary<string, ModInstallationState> Mods
)
{
    public static InstallationState Empty() => new(
        Time: null,
        Mods: ImmutableDictionary.Create<string, ModInstallationState>()
    );
};

public record ModInstallationState(
    // TODO: nullable for backward compatibility
    DateTime? Time,
    // Unknown when partially installed or upgrading from a previous version
    int? FsHash,
    // TODO: needed for backward compatibility
    // infer from null hash after the first install
    bool Partial,
    IReadOnlyCollection<string> Files
)
{
    public static ModInstallationState Empty => new(null, null, false, Array.Empty<string>());
}
