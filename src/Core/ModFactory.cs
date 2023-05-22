using Core.Mods;
using Core.Games;

namespace Core;

public class ModFactory : IModFactory
{
    public interface IConfig : ManualInstallMod.IConfig
    {
    }

    private readonly IConfig config;
    private readonly IGame game;

    public ModFactory(IConfig config, IGame game)
    {
        this.config = config;
        this.game = game;
    }

    public IMod ManualInstallMod(string packageName, string extractedPath) =>
        new ManualInstallMod(packageName, extractedPath, config);

    public IMod GeneratedBootfiles(string generationBasePath) =>
        new GeneratedBootfiles(game.InstallationDirectory, generationBasePath);
}
