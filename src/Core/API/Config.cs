using Core.Games;
using Core.SoftwareUpdates;
using Microsoft.Extensions.Configuration;

namespace Core.API;

public class Config
{
    public GitHubUpdateChecker.IConfig Updates { get; set; } = new UpdateConfig();
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

public class UpdateConfig : GitHubUpdateChecker.IConfig
{
    public string GitHubOwner { get; set; } = "OpenSimTools";
    public string GitHubRepo { get; set; } = "AMS2CM";
    public string GitHubClientApp { get; set; } = "AMS2CMUpdateChecker";
}

public class GameConfig : Game.IConfig
{
    public string SteamId { get; set; } = "Undefined";
    public string Path { get; set; } = ".";
    public string ProcessName { get; set; } = "Undefined";
}

public class ModInstallConfig : InstallationFactory.IConfig
{
    public IEnumerable<string> DirsAtRoot { get; set; } = Array.Empty<string>();
    public IEnumerable<string> ExcludedFromInstall { get; set; } = Array.Empty<string>();
    public IEnumerable<string> ExcludedFromConfig { get; set; } = Array.Empty<string>();
}
