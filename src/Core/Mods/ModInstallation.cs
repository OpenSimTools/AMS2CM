namespace Core.Mods;

internal record ModInstallation
(
    string PackageName,
    IInstallation.State Installed,
    IReadOnlyCollection<string> InstalledFiles,
    int? PackageFsHash
) : IInstallation;
