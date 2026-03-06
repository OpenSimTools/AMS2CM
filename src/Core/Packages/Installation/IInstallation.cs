using Core.Utils;

namespace Core.Packages.Installation;

public interface IInstallation : IPackageInfo
{
    IReadOnlyCollection<string> PackageDependencies { get; }

    IReadOnlyCollection<RootedPath> InstalledFiles { get; }
    State Installed { get; }

    enum State
    {
        NotInstalled = 0,
        PartiallyInstalled = 1,
        Installed = 2
    }
}
