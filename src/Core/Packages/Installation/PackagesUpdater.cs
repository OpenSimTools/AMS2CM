using System.Collections.Immutable;
using Core.Packages.Installation.Installers;
using Core.Packages.Repository;
using Core.Utils;

namespace Core.Packages.Installation;

public class PackagesUpdater<TEventHandler>
{
    private readonly IInstallationsUpdater<TEventHandler> installationsUpdater;
    private readonly ITempDir tempDir;
    private readonly BaseInstaller.IConfig installerConfig;

    public PackagesUpdater(IInstallationsUpdater<TEventHandler> installationsUpdater, ITempDir tempDir, BaseInstaller.IConfig installerConfig)
    {
        this.installationsUpdater = installationsUpdater;
        this.tempDir = tempDir;
        this.installerConfig = installerConfig;
    }

    public void Apply(IReadOnlyDictionary<string, PackageInstallationState> previousState, IEnumerable<Package> packages, string installDir, Action<IReadOnlyDictionary<string, PackageInstallationState>> afterInstall,
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
                state =>
                {
                    switch (state.Installed)
                    {
                        case IInstallation.State.Installed:
                        case IInstallation.State.PartiallyInstalled:
                            currentState.Upsert(state.PackageName,
                                existing => existing with
                                {
                                    Partial = state.Installed == IInstallation.State.PartiallyInstalled,
                                    Files = state.InstalledFiles
                                },
                                () => new PackageInstallationState(
                                    Time: DateTime.Now,
                                    FsHash: state.PackageFsHash,
                                    Partial: state.Installed == IInstallation.State.PartiallyInstalled,
                                    Files: state.InstalledFiles
                                ));
                            break;
                        case IInstallation.State.NotInstalled:
                            currentState.Remove(state.PackageName);
                            break;
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
            ? new DirectoryInstaller(package.Name, package.FsHash, tempDir, installerConfig, package.FullPath)
            : new ArchiveInstaller(package.Name, package.FsHash, tempDir, installerConfig, package.FullPath);
}
