namespace Core;

public record ModState(
    string ModName,
    string PackageName,
    string? PackagePath,
    bool? IsInstalled, // null is partial
    bool IsEnabled,
    bool IsOutOfDate
);
