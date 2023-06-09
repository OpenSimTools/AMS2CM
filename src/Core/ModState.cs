namespace Core;

public record ModState(
    string PackageName,
    string? PackagePath,
    bool IsInstalled,
    bool IsEnabled
);
