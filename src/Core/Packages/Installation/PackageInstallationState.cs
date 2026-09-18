namespace Core.Packages.Installation;

public record PackageInstallationState(
    DateTime Time,
    // Unknown when partially installed or upgrading from a previous version
    int? VersionHash,
    bool Partial,
    IReadOnlyCollection<string> Dependencies,
    IReadOnlyCollection<string> Files,
    IReadOnlyCollection<string> ShadowedBy
);
