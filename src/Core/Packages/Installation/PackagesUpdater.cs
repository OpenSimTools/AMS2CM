using System.Collections.Immutable;
using Core.Packages.Installation.Installers;
using Core.Packages.Repository;
using Core.Utils;

namespace Core.Packages.Installation;

public class PackagesUpdater<TEventHandler>
{
    private readonly IInstallationsUpdater<TEventHandler> installationsUpdater;

    public PackagesUpdater(IInstallationsUpdater<TEventHandler> installationsUpdater)
    {
        this.installationsUpdater = installationsUpdater;
    }

    public void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> previousState,
        IEnumerable<Package> packages,
        string installDir,
        Action<IReadOnlyDictionary<string, PackageInstallationState>> afterInstall,
        TEventHandler eventHandler, CancellationToken cancellationToken)
    {
        var installers = packages.Select(PackageInstaller).ToImmutableArray();

        var currentState = new Dictionary<string, PackageInstallationState>(previousState);
        try
        {
            installationsUpdater.Apply(
                previousState,
                installers,
                installDir,
                (packageName, state) =>
                {
                    if (state is null)
                    {
                        currentState.Remove(packageName);
                    }
                    else
                    {
                        currentState[packageName] = state;
                    }
                },
                eventHandler,
                cancellationToken);
        }
        finally
        {
            afterInstall(currentState);
        }
    }

    private IInstaller PackageInstaller(Package package) =>
        Directory.Exists(package.FullPath)
            ? new DirectoryInstaller(package.Name, package.FsHash, package.FullPath)
            : new ArchiveInstaller(package.Name, package.FsHash, package.FullPath);
}
