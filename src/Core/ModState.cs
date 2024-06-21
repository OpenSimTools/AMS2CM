namespace Core;

public record ModState(
    string PackageName,
    string? PackagePath,
    bool? IsInstalled, // null is partial
    bool IsEnabled,
    bool IsOutOfDate
);
