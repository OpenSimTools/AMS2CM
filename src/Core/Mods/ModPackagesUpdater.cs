using System.Collections.Immutable;
using Core.State;

namespace Core.Mods;

public class ModPackagesUpdater : IModPackagesUpdater
{
    private readonly IInstallationsUpdater installationsUpdater;
    private readonly ITempDir tempDir;
    private readonly BaseInstaller.IConfig installerConfig;

    public ModPackagesUpdater(IInstallationsUpdater installationsUpdater, ITempDir tempDir, BaseInstaller.IConfig installerConfig)
    {
        this.installationsUpdater = installationsUpdater;
        this.tempDir = tempDir;
        this.installerConfig = installerConfig;
    }

    public void Apply(IReadOnlyDictionary<string, ModInstallationState> currentState, IEnumerable<ModPackage> packages, string installDir, Action<IInstallation> afterInstall,
        InstallationsUpdater.IEventHandler eventHandler, CancellationToken cancellationToken)
    {
        var installers = packages.Select(ModInstaller).ToImmutableArray();
        installationsUpdater.Apply(currentState, installers, installDir, afterInstall, eventHandler, cancellationToken);
    }

    private IInstaller ModInstaller(ModPackage modPackage) =>
        Directory.Exists(modPackage.FullPath)
            ? new ModDirectoryInstaller(modPackage.PackageName, modPackage.FsHash, tempDir, installerConfig, modPackage.FullPath)
            : new ModArchiveInstaller(modPackage.PackageName, modPackage.FsHash, tempDir, installerConfig, modPackage.FullPath);
}
