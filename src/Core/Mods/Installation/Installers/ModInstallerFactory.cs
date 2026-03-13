using Core.Games;
using Core.Packages.Installation.Installers;
using Core.Utils;

namespace Core.Mods.Installation.Installers;

public class ModInstallerFactory<TConfig> : IModInstallerFactory<BootfilesInstaller.IEventHandler>
    where TConfig : ModInstaller.IConfig, BootfilesInstaller.IConfig
{
    private readonly IGame game;
    private readonly ITempDir tempDir;
    private readonly IBootfilesNaming bootfilesNaming;
    private readonly TConfig config;

    public ModInstallerFactory(IGame game,
        ITempDir tempDir,
        IBootfilesNaming bootfilesNaming,
        TConfig config)
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
