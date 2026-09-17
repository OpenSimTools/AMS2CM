namespace Core.Packages.Installation;

public record PackageInstallationState(
    DateTime Time,
    // Unknown when partially installed or upgrading from a previous version
    int? VersionHash,
    IReadOnlyCollection<string> Dependencies,
    IReadOnlyCollection<string> Files,
    IReadOnlyCollection<string> ShadowedBy
) {
    public bool Partial => VersionHash is null; // && Files.Count != 0
}
