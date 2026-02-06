using Core.Games;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation.Installers;

public class ModInstallerFactory : IModInstallerFactory<BootfilesInstaller.IEventHandler>
{
    internal const string BootfilesPrefix = "__bootfiles";

    private readonly IGame game;
    private readonly ITempDir tempDir;
    private readonly ModInstaller.IConfig config;

    public ModInstallerFactory(IGame game,
        ITempDir tempDir,
        ModInstaller.IConfig config)
    {
        this.game = game;
        this.tempDir = tempDir;
        this.config = config;
    }

    public IInstaller ModInstaller(IInstaller packageInstaller, IInstaller bootfilesInstaller) =>
        new ModInstaller(packageInstaller, bootfilesInstaller.PackageName, game, tempDir, config);

    public IInstaller BootfilesInstaller(IInstaller? bootfilesPackageInstaller, BootfilesInstaller.IEventHandler eventHandler) =>
        new BootfilesInstaller(bootfilesPackageInstaller, game, tempDir, eventHandler, config);

    public bool IsBootFiles(string packageName) =>
        packageName.StartsWith(BootfilesPrefix);
}
