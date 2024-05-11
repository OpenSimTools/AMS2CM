﻿namespace Core.Mods;

public interface IModInstallation
{
    string PackageName { get; }
    State Installed { get; }
    IReadOnlyCollection<string> InstalledFiles { get; }
    int? PackageFsHash { get; }

    public enum State
    {
        NotInstalled = 0,
        PartiallyInstalled = 1,
        Installed = 2
    }
}