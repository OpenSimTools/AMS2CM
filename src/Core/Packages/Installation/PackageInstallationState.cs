namespace Core.Packages.Installation;

public record PackageInstallationState(
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
    public static PackageInstallationState Empty => new(null, null, false, Array.Empty<string>());
}
