namespace Core.Mods;

internal record ModInstallation
(
    string PackageName,
    IModInstallation.State Installed,
    IReadOnlyCollection<string> InstalledFiles,
    int? PackageFsHash
) : IModInstallation;