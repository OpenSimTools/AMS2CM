using Core.Utils;

namespace Core.Packages.Installation.Installers;

public interface IInstallation
{
    IReadOnlySet<RootedPath> InstalledFiles { get; }
    State Installed { get; }
    DateTime InstallTime { get; }

    enum State
    {
        NotInstalled = 0,
        PartiallyInstalled = 1,
        Installed = 2
    }
}
