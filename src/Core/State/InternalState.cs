using System.Collections.Immutable;

namespace Core.State;

public record InternalState(
        InternalInstallationState Install
    )
{
    public static InternalState Empty() => new(
        Install: InternalInstallationState.Empty()
    );
};

public record InternalInstallationState(
    // TODO: needed for backward compatibility
    DateTime? Time,
    IReadOnlyDictionary<string, InternalModInstallationState> Mods
)
{
    public static InternalInstallationState Empty() => new(
        Time: null,
        Mods: ImmutableDictionary.Create<string, InternalModInstallationState>()
    );
};

public record InternalModInstallationState(
    // TODO: nullable for backward compatibility
    DateTime? Time,
    // Unknown when partially installed or upgrading from a previous version
    int? FsHash,
    // TODO: needed for backward compatibility
    // infer from null hash after the first install
    bool Partial,
    IReadOnlyCollection<string> Files
);
