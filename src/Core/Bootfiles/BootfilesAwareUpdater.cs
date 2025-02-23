using System.Collections.Immutable;
using Core.Games;
using Core.Mods;
using Core.State;
using Core.Utils;

namespace Core.Bootfiles;

public class BootfilesAwareUpdater<TEventHandler> : IInstallationsUpdater<TEventHandler>
    where TEventHandler : BootfilesInstaller.IEventHandler
{
    private readonly IInstallationsUpdater<TEventHandler> inner;

    private readonly IGame game;
    private readonly ITempDir tempDir;
    private readonly BaseInstaller.IConfig installerConfig;

    public BootfilesAwareUpdater(IInstallationsUpdater<TEventHandler> inner, IGame game, ITempDir tempDir, BaseInstaller.IConfig installerConfig)
    {
        this.inner = inner;
        this.game = game;
        this.tempDir = tempDir;
        this.installerConfig = installerConfig;
    }

    public void Apply(IReadOnlyDictionary<string, ModInstallationState> currentState,
        IReadOnlyCollection<IInstaller> packageInstallers, string installDir, Action<IInstallation> afterInstall,
        TEventHandler eventHandler, CancellationToken cancellationToken)
    {
        var (bootfiles, notBootfiles) = packageInstallers.Partition(p => BootfilesManager.IsBootFiles(p.PackageName));

        var allInstallers = notBootfiles.Append(CreateBootfilesInstaller(bootfiles, eventHandler)).ToImmutableArray();

        inner.Apply(currentState, allInstallers, installDir, afterInstall, eventHandler, cancellationToken);
    }

    private BootfilesInstaller CreateBootfilesInstaller(IEnumerable<IInstaller> bootfilesPackageInstallers, TEventHandler eventHandler)
    {
        var installer = bootfilesPackageInstallers.FirstOrDefault() ??
                        new GeneratedBootfilesInstaller(tempDir, installerConfig, game);
        return new BootfilesInstaller(installer, eventHandler);
    }
}
