using System.Collections.Immutable;
using Core.Games;
using Core.Mods.Installation.Installers;
using Core.Packages.Installation;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation;

public class ModInstallationsUpdater<TEventHandler> : IInstallationsUpdater<TEventHandler>
    where TEventHandler : BootfilesInstaller.IEventHandler
{
    #region TODO Move to a better place when not called all over the place

    public const string BootfilesPrefix = "__bootfiles";

    internal static bool IsBootFiles(string packageName) =>
        packageName.StartsWith(BootfilesPrefix);

    #endregion

    private readonly IInstallationsUpdater<TEventHandler> inner;

    private readonly IGame game;
    private readonly ITempDir tempDir;
    private readonly ModInstaller.IConfig config;

    public ModInstallationsUpdater(IInstallationsUpdater<TEventHandler> inner, IGame game, ITempDir tempDir, ModInstaller.IConfig config)
    {
        this.inner = inner;
        this.game = game;
        this.tempDir = tempDir;
        this.config = config;
    }

    public void Apply(
        IReadOnlyDictionary<string, PackageInstallationState> currentState,
        IReadOnlyCollection<IInstaller> packageInstallers,
        string installDir,
        Action<string, PackageInstallationState?> afterInstall,
        TEventHandler eventHandler,
        CancellationToken cancellationToken)
    {
        var (bootfiles, notBootfiles) = packageInstallers.Partition(p => IsBootFiles(p.PackageName));

        var allInstallers = notBootfiles
            .Select(i => new ModInstaller(i, game, tempDir, config))
            .Append(CreateBootfilesInstaller(bootfiles, eventHandler)).ToImmutableArray();

        inner.Apply(currentState, allInstallers, installDir, afterInstall, eventHandler, cancellationToken);
    }

    private IInstaller CreateBootfilesInstaller(IEnumerable<IInstaller> bootfilesPackageInstallers, TEventHandler eventHandler)
    {
        var installer = bootfilesPackageInstallers.FirstOrDefault();
        return new BootfilesInstaller(installer, game, tempDir, eventHandler, config);
    }
}
