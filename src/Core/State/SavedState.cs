using System.Collections.Immutable;
using Core.Packages.Installation;

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
    IReadOnlyDictionary<string, PackageInstallationState> Mods
)
{
    public static InstallationState Empty() => new(
        Time: null,
        Mods: ImmutableDictionary.Create<string, PackageInstallationState>()
    );
}
