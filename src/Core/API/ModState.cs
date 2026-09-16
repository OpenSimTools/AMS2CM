namespace Core.API;

public record ModState(
    string PackageName,
    string? PackageLocation,
    bool? IsInstalled, // null is partial
    bool IsEnabled,
    bool IsOutOfDate
);
