using Core.Games;
using Microsoft.Extensions.Configuration;

namespace Core;

public class Config
{
    public Game.IConfig Game { get; set; } = new GameConfig();
    public ModInstallConfig ModInstall { get; set; } = new ModInstallConfig();

    public static Config Load(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddYamlFile("Config.yaml")
            .AddCommandLine(args)
            .Build();
        return config.Get<Config>() ?? throw new Exception("Failed to read configuration");
    }
}

public class GameConfig : Game.IConfig
{
    public string SteamId { get; set; } = "Undefined";
    public string Path { get; set; } = ".";
    public string ProcessName { get; set; } = "Undefined";
}

public class ModInstallConfig : ModFactory.IConfig
{
    public IEnumerable<string> DirsAtRoot { get; set; } = Array.Empty<string>();
    public IEnumerable<string> ExcludedFromConfig { get; set; } = Array.Empty<string>();
}
