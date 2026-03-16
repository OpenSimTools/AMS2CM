using Core.Utils;

namespace Core.Packages.Installation;

public interface IInstallation : IPackageInfo
{
    IReadOnlySet<string> PackageDependencies { get; }

    IReadOnlySet<RootedPath> InstalledFiles { get; }
    State Installed { get; }

    enum State
    {
        NotInstalled = 0,
        PartiallyInstalled = 1,
        Installed = 2
    }
}
