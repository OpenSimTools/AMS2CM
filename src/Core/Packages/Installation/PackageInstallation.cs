namespace Core.Packages.Installation;

internal record PackageInstallation
(
    string PackageName,
    IInstallation.State Installed,
    IReadOnlyCollection<string> InstalledFiles,
    int? PackageFsHash
) : IInstallation;
