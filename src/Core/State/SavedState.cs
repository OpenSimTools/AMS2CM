using System.Collections.Immutable;
using Core.Packages.Installation;

namespace Core.State;

public record SavedState(
    IReadOnlyDictionary<string, PackageInstallationState> Installation
)
{
    public static SavedState Empty() => new SavedState(
        Installation: ImmutableDictionary.Create<string, PackageInstallationState>()
    );
}
