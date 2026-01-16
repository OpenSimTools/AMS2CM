using Newtonsoft.Json;

namespace Core.Packages.Installation;

public record PackageInstallationState(
    DateTime Time,
    // Unknown when partially installed or upgrading from a previous version
    int? FsHash,
    // TODO: needed for backward compatibility
    // infer from null hash after the first install
    bool Partial,
    IReadOnlyCollection<string> Dependencies,
    IReadOnlyCollection<string> Files,
    IReadOnlyCollection<string> ShadowedBy
);
