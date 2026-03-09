using Core.Games;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation.Installers;

public class ModInstallerFactory : IModInstallerFactory<BootfilesInstaller.IEventHandler>
{
    private readonly IGame game;
    private readonly ITempDir tempDir;
    private readonly IBootfilesNaming bootfilesNaming;
    private readonly ModInstaller.IConfig config;

    public ModInstallerFactory(IGame game,
        ITempDir tempDir,
        IBootfilesNaming bootfilesNaming,
        ModInstaller.IConfig config)
    {
        this.game = game;
        this.tempDir = tempDir;
        this.bootfilesNaming = bootfilesNaming;
        this.config = config;
    }

    public IInstaller ModInstaller(IInstaller packageInstaller, IInstaller bootfilesInstaller) =>
        new ModInstaller(packageInstaller, tempDir.BasePath, config, game.InstallationDirectory, bootfilesInstaller.PackageName);

    public IInstaller BootfilesInstaller(IInstaller? bootfilesPackageInstaller,
        BootfilesInstaller.IEventHandler eventHandler) =>
        new BootfilesInstaller(bootfilesPackageInstaller, tempDir.BasePath, config,
            game.InstallationDirectory, bootfilesNaming, eventHandler);
}
