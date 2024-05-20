﻿namespace Core.Mods;

public interface IInstallation
{
    string PackageName { get; }
    int? PackageFsHash { get; }

    IReadOnlyCollection<string> InstalledFiles { get; }
    State Installed { get; }
    enum State
    {
        NotInstalled = 0,
        PartiallyInstalled = 1,
        Installed = 2
    }
}